using Minio.DataModel;
using ProjectManager.API.Services.LexorankService;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using Xunit;

namespace ProjectManager.Tests
{
    public class LexorankServiceTests
    {
        private readonly LexorankService _service = new();

        //GetInitialPosition

        [Fact]
        public void GetInitialPosition_NullLastPosition_ReturnsDefaultMiddle()
        {
            //Üres oszlopba kerül az első task, nincs előző pozíció, az alapértelmezett középső karaktert (0|i) kell kapni.
            var result = _service.GetInitialPosition(null);
            Assert.Equal("0|i", result);
        }

        [Fact]
        public void GetInitialPosition_WithLastPosition_IncrementsPosistionPart()
        {
            //Egy már meglévő task után kerül új task az oszlop végére, a visszaadott pozíciónak nagyobbnak kell lennie mint az előző.
            var result = _service.GetInitialPosition("0|i");
            Assert.StartsWith("0|", result);
            Assert.True(string.Compare(result, "0|i") > 0);
        }

        [Fact]
        public void GetInitialPosition_InheritsBucketFromLastPosition()
        {
            //Ha az oszlop már rebalance-olt és 1| bucketben van, az új task is örökölje a 1| bucketet, ne kerüljön vissza 0|-ra.
            var result = _service.GetInitialPosition("1|i");
            Assert.StartsWith("1|", result);
        }
        
        //GetMiddle

        [Fact]
        public void GetMiddle_BothNull_ReturnsDefaultMiddle()
        {
            //Teljesen üres oszlopba kerülő task, nincs se előző se következő, alapértelmezett 0|i pozíciót kell kapnia.
            var result = _service.GetMiddle(null, null);
            Assert.Equal("0|i", result);
        }

        [Fact]
        public void GetMiddle_BetweenTwoPositions_ReturnsBetween()
        {
            //Közbeszúrás: két egymástól távol lévő pozíció közé kerül egy task, az eredmény mindkettőnél nagyobbnak illetve kisebbnek kell legyen.
            var prev = "0|a";
            var next = "0|z";
            var result = _service.GetMiddle(prev, next);

            Assert.True(string.Compare(result, prev) > 0);
            Assert.True(string.Compare(result, next) < 0);
        }

        [Fact]
        public void GetMiddle_NullPrev_ReturnsBeforeNext()
        {
            //Task az oszlop elejére kerül, nincs előző szomszéd, az eredménynek kisebbnek kell lennie mint a jelenlegi első elem.
            var next = "0|z";
            var result = _service.GetMiddle(null, next);

            Assert.True(string.Compare(result, next) < 0);
        }

        [Fact]
        public void GetMiddle_NullNext_ReturnsAfterPrev()
        {
            //Task az oszlop végére kerül, nincs következő szomszéd, az eredménynek nagyobbnak kell lennie mint az utolsó elem.
            var prev = "0|a";
            var result = _service.GetMiddle(prev, null);

            Assert.True(string.Compare(result, prev) > 0);
        }

        [Fact]
        public void GetMiddle_InheritsBucketFromPrev()
        {
            //Közbeszúrásnál a bucket öröklés ellenőrzése, ha 2| bucketben vannak a szomszédok, az új pozíció is 2| bucketet kapjon.
            var prev = "2|a";
            var next = "2|z";
            var result = _service.GetMiddle(prev, next);

            Assert.StartsWith("2|", result);
        }

        [Fact]
        public void GetMiddle_InheritsBucketFromNextWhenPrevNull()
        {
            //Oszlop elejére szúrásnál bucket öröklés, ha a következő szomszéd 1| bucketben van és nincs előző szomszéd, az eredmény is 1| bucketet kapjon.
            var next = "1|z";
            var result = _service.GetMiddle(null, next);

            Assert.StartsWith("1|", result);
        }

        //GetBefore indirekt teszt (null prev)
        [Fact]
        public void GetMiddle_NullPrev_ResultNotLongerThanNext()
        {
            //Ha az oszlop elejére kerül egy task, a generált pozíció ne legyen hosszabb string mint a jelenlegi első elem,
            //a GetBefore ne toldjon felesleges karaktereket.
            var next = "0|z";
            var result = _service.GetMiddle(null, next);

            var resultPart = result.Split('|')[1];
            var nextPart = next.Split('|')[1];

            Assert.True(string.Compare(result, next) < 0);
            Assert.True(resultPart.Length <= nextPart.Length);
        }

        //IncrementPosition indirekt teszt (null next)
        [Fact]
        public void GetMiddle_NullNext_ResultIsIncrementOfPrev()
        {
            //Ha az oszlop végére kerül task, pontosan eggyel nagyobb pozíciót kapjon:
            //0|i után 0|j legyen, nem több karakteres string.
            var prev = "0|i";
            var result = _service.GetMiddle(prev, null);

            Assert.True(string.Compare(result, prev) > 0);
            Assert.Equal("0|j", result); // "i" + 1 = "j" base36-ban
        }

        //GetBetween (GetMiddle használatával)

        [Fact]
        public void GetMiddle_VeryClosePositions_IncreasesLength()
        {
            //Két szomszédos karakter közé szúrás(0|a és 0|b),
            //mivel a = 10 és b = 11 között nincs egész,
            //a service-nek hosszabb stringet kell generálnia, nem egyszerűen hozzáfűzni a középső karaktert.
            var prev = "0|a";
            var next = "0|b";
            var result = _service.GetMiddle(prev, next);

            Assert.True(string.Compare(result, prev) > 0);
            Assert.True(string.Compare(result, next) < 0);
            // Eredmény hosszabb mint 1 karakter a pozíció részben
            var positionPart = result.Split('|')[1];
            Assert.True(positionPart.Length > 1);
        }

        [Fact]
        public void GetMiddle_ManyInsertions_NeverThrows()
        {
            //Stressz teszt: 50-szer egymás után ugyanoda szúrunk be: (0|a és 0|b közé)
            //Ez a BigInteger számítás kritikus tesztje, a régi long alapú kód nagyjából a 12. iterációnál hibás értéket adott volna vissza.
            var first = "0|a";
            var second = "0|b";

            var current = first;
            for (int i = 0; i < 50; i++)
            {
                var middle = _service.GetMiddle(current, second);
                Assert.True(string.Compare(middle, current) > 0);
                Assert.True(string.Compare(middle, second) < 0);
                current = middle;
            }
        }

        //RebalancePositions 100 task egyediség
        [Fact]
        public void RebalancePositions_100Tasks_AllPositionsUnique()
        {
            //A régi long alapú bug direkt tesztje: 36 - nál több tasknál(step = 36 / (count + 1) képlettel) duplikált pozíciók keletkeztek volna.
            //100 taskra az összes pozíció egyedi legyen.
            var result = _service.RebalancePositions(100, 0);
            Assert.Equal(100, result.Count);
            
            var distinct = result.Distinct().ToList();
            Assert.Equal(100, distinct.Count);
        }
        
        //NeedsRebalancing

        [Fact]
        public void NeedsRebalancing_ShortPosition_ReturnsFalse()
        {
            //Normál, rövid pozíciónál (0|i) ne triggerelődjön rebalancing.
            Assert.False(_service.NeedsRebalancing("0|i"));
        }

        [Fact]
        public void NeedsRebalancing_LongPosition_ReturnsTrue()
        {
            //51 karakter hosszú pozíció stringnél rebalancing szükséges, ezt jelezze a metódus.
            var longPos = "0|" + new string('a', 51);
            Assert.True(_service.NeedsRebalancing(longPos));
        }

        [Fact]
        public void NeedsRebalancing_Exactly51Chars_ReturnsTrue()
        {
            //Határeset: pontosan 51 karakter már meghaladja a limitet, rebalancing szükséges.
            var pos = "0|" + new string('a', 51);
            Assert.True(_service.NeedsRebalancing(pos));
        }

        //NeedsRebalancing határesetek

        [Fact]
        public void NeedsRebalancing_Exactly50Chars_ReturnsFalse()
        {
            //Határeset: pontosan 50 karakter még belefér a limitbe, ne triggerelődjön rebalancing.
            var pos = "0|" + new string('a', 50);
            Assert.False(_service.NeedsRebalancing(pos));
        }

        //HasCollision

        [Fact]
        public void HasCollision_SamePositions_ReturnsTrue()
        {
            //Két azonos pozíció ütközést jelent, ez triggereli a rebalancing-et MoveTaskAsync-ban.
            Assert.True(_service.HasCollision("0|i", "0|i"));
        }

        [Fact]
        public void HasCollision_DifferentPositions_ReturnsFalse()
        {
            //Két különböző pozíció esetén nincs ütközés, a rebalancing ne triggerelődjön feleslegesen.
            Assert.False(_service.HasCollision("0|a", "0|z"));
        }

        //RebalancePositions

        [Fact]
        public void RebalancePositions_ReturnsCorrectCount()
        {
            //5 taskra kért rebalancing pontosan 5 pozíciót adjon vissza, ne generáljon többet vagy kevesebbet.
            var result = _service.RebalancePositions(5, 0);
            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void RebalancePositions_PositionsAreOrdered()
        {
            //A rebalancing után a pozíciók sorrendben legyenek, a taskok sorrendje ne vesszen el az új pozíciók alapján.
            var result = _service.RebalancePositions(5, 0);
            for (int i = 0; i < result.Count - 1; i++)
            {
                Assert.True(string.Compare(result[i], result[i + 1]) < 0);
            }
        }

        [Fact]
        public void RebalancePositions_UsesCorrectBucket()
        {
            //A megadott bucket száma jelenjen meg minden generált pozícióban, rebalancing után ne keveredjenek a bucketek.
            var result = _service.RebalancePositions(3, 2);
            Assert.All(result, pos => Assert.StartsWith("2|", pos));
        }

        [Fact]
        public void RebalancePositions_PositionsAreEvenlyDistributed()
        {
            //Az első és utolsó generált pozíció ne legyen azonos, az elosztás tényleg lefedi a skálát, ne torlódjanak össze az értékek.
            var result = _service.RebalancePositions(10, 0);
            Assert.Equal(10, result.Count);
            // Első és utolsó pozíció nem lehet azonos
            Assert.NotEqual(result[0], result[9]);
        }

        //GetBucket / GetNextBucket

        [Fact]
        public void GetBucket_ReturnsCorrectBucket()
        {
            //Különböző bucketű pozíciókból(0|, 1|, 2|) helyesen olvassa ki a bucket számát.
            Assert.Equal(0, _service.GetBucket("0|i"));
            Assert.Equal(1, _service.GetBucket("1|abc"));
            Assert.Equal(2, _service.GetBucket("2|xyz"));
        }

        [Fact]
        public void GetNextBucket_WrapsAround()
        {
            //A bucket rotáció körkörösen működik: 0, 1, 2, 0 sorrendben: 2 után ne 3-at adjon vissza hanem 0-t.
            Assert.Equal(1, _service.GetNextBucket(0));
            Assert.Equal(2, _service.GetNextBucket(1));
            Assert.Equal(0, _service.GetNextBucket(2)); //Körkörös működés.
        }
    }
}
