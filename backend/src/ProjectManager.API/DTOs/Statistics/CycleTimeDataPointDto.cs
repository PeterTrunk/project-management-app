namespace ProjectManager.API.DTOs.Statistics
{
    /// <summary>
    /// Egy feladat átfutási ideje: az első hozzá kapcsolt committól a pull request
    /// összeolvadásáig.
    ///
    /// Ez a kimutatás abban különbözik a többi ötől, hogy nem a feladatkezelésből dolgozik,
    /// hanem a <b>tényleges munkavégzésből</b>: a commit időpontja a verziókezelőből jön, az
    /// összeolvadás pedig a szolgáltatótól. Cserébe csak arra a feladatra áll elő, amelyhez
    /// van is commit és összeolvadt pull request.
    ///
    /// A nyers időpontok azért utaznak a számított órák MELLETT, mert a felület az
    /// összeolvadás napja szerint helyezi el a pontokat, az összegzést (középérték, átlag,
    /// leghosszabb) pedig a kliens számolja - ugyanúgy, ahogy a kimutatások egyike sem
    /// tárolódik, hanem lekérdezéskor áll elő.
    /// </summary>
    public class CycleTimeDataPointDto
    {
        public Guid TaskId { get; set; }
        public string TaskKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>A feladathoz kapcsolt legkorábbi commit ideje (a commit saját időbélyege).</summary>
        public DateTime FirstCommitAt { get; set; }

        /// <summary>A legutóbb összeolvadt pull request összeolvadásának ideje.</summary>
        public DateTime MergedAt { get; set; }

        /// <summary>
        /// A kettő között eltelt idő órában. <b>Lehet negatív</b>, és ez nem hiba: a commit
        /// időbélyege a fejlesztő gépéről jön (a git engedi átírni), és egy commit utólag is
        /// hozzárendelhető egy már összeolvadt pull requesthez. A szűrés ezért a megjelenítés
        /// dolga, nem a lekérdezésé - a lekérdezés nem dönt arról, mit nem mutatunk meg.
        /// </summary>
        public double Hours { get; set; }

        /// <summary>Ahány commit tartozik a feladathoz - a pont méretéhez és a buboréksúgóhoz.</summary>
        public int CommitCount { get; set; }

        /// <summary>Az összeolvadt pull request sorszáma.</summary>
        public int PrNumber { get; set; }
    }
}
