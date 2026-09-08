using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Options;
using System.Security.Cryptography;
using EncryptionSvc = ProjectManager.API.Services.EncryptionService.EncryptionService;

namespace ProjectManager.Tests.Services
{
    /// <summary>
    /// A szolgáltatás a git integrációk access tokenjeit védi az adatbázisban. A tesztek a
    /// hitelesített titkosítás két lényegi tulajdonságát mérik: a nonce újrafelhasználás
    /// hiányát, és azt, hogy a megbuherált adat hibát dob, nem pedig szemetet ad vissza.
    /// </summary>
    public class EncryptionServiceTests
    {
        //Determinisztikus kulcs: 32 bájt, 0..31 értékekkel
        private static readonly string ValidKey =
            Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

        private static EncryptionSvc CreateSut(string? key = null) =>
            new(Options.Create(new EncryptionOptions { Key = key ?? ValidKey }));

        //Konstruktor

        [Theory]
        [InlineData(16)]
        [InlineData(24)]
        [InlineData(31)]
        [InlineData(33)]
        [InlineData(64)]
        public void Ctor_KeyIsNot32Bytes_Throws(int byteLength)
        {
            var key = Convert.ToBase64String(new byte[byteLength]);

            var ex = Assert.Throws<InvalidOperationException>(() => CreateSut(key));
            Assert.Contains("32 bytes", ex.Message);
        }

        [Fact]
        public void Ctor_EmptyKey_Throws()
        {
            //Üres konfiguráció: a base64 dekódolás 0 bájtot ad, ami nem 32
            Assert.Throws<InvalidOperationException>(() => CreateSut(""));
        }

        [Fact]
        public void Ctor_KeyIsNotBase64_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => CreateSut("nem-base64-kulcs!!!"));
        }

        [Fact]
        public void Ctor_ValidKey_Succeeds()
        {
            Assert.NotNull(CreateSut());
        }

        //Oda-vissza

        [Theory]
        [InlineData("ghp_16C7e42F292c6912E7710c838347Ae178B4a")]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("árvíztűrő tükörfúrógép")]
        [InlineData("sor1\nsor2\ttab")]
        public void EncryptThenDecrypt_ReturnsTheOriginal(string plaintext)
        {
            var sut = CreateSut();

            Assert.Equal(plaintext, sut.Decrypt(sut.Encrypt(plaintext)));
        }

        [Fact]
        public void EncryptThenDecrypt_LongValue_ReturnsTheOriginal()
        {
            var sut = CreateSut();
            var plaintext = new string('x', 10_000);

            Assert.Equal(plaintext, sut.Decrypt(sut.Encrypt(plaintext)));
        }

        [Fact]
        public void Decrypt_WorksAcrossInstances()
        {
            //Az éles életben a titkosítás és a visszafejtés más kérésben, más példányon fut
            var ciphertext = CreateSut().Encrypt("titok");

            Assert.Equal("titok", CreateSut().Decrypt(ciphertext));
        }

        //Nonce

        [Fact]
        public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
        {
            var sut = CreateSut();

            Assert.NotEqual(sut.Encrypt("ugyanaz"), sut.Encrypt("ugyanaz"));
        }

        [Fact]
        public void Encrypt_ManyTimes_NeverRepeatsTheCiphertext()
        {
            //A nonce újrafelhasználás AES-GCM-nél kulcsvesztéssel ér fel
            var sut = CreateSut();
            var results = Enumerable.Range(0, 500).Select(_ => sut.Encrypt("ugyanaz")).ToHashSet();

            Assert.Equal(500, results.Count);
        }

        //Prefix

        [Fact]
        public void Encrypt_PrefixesTheOutput()
        {
            Assert.StartsWith(EncryptionSvc.Prefix, CreateSut().Encrypt("titok"));
        }

        [Fact]
        public void IsEncrypted_PrefixedValue_ReturnsTrue()
        {
            Assert.True(EncryptionSvc.IsEncrypted(CreateSut().Encrypt("titok")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("sima szöveg")]
        [InlineData("ghp_16C7e42F292c6912E7710c838347Ae178B4a")]
        [InlineData("ENC:V1:valami")]
        public void IsEncrypted_UnprefixedValue_ReturnsFalse(string? value)
        {
            //A prefix illesztés Ordinal, tehát a nagybetűs változat sem számít titkosítottnak
            Assert.False(EncryptionSvc.IsEncrypted(value));
        }

        [Fact]
        public void Decrypt_LegacyValueWithoutPrefix_StillWorks()
        {
            //A prefix bevezetése előtt keletkezett sorok migráció nélkül is olvashatók kell maradjanak
            var sut = CreateSut();
            var legacy = sut.Encrypt("régi token")[EncryptionSvc.Prefix.Length..];

            Assert.False(EncryptionSvc.IsEncrypted(legacy));
            Assert.Equal("régi token", sut.Decrypt(legacy));
        }

        //Sérült vagy hamisított adat

        [Fact]
        public void Decrypt_TamperedTag_Throws()
        {
            var sut = CreateSut();
            var tampered = FlipByte(sut.Encrypt("titok"), fromEnd: 1);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Decrypt(tampered));
            Assert.Contains("Decryption failed", ex.Message);
        }

        [Fact]
        public void Decrypt_TamperedCiphertext_Throws()
        {
            //A hitelesítés lényege: a módosított rejtett szöveg nem szemetet ad, hanem hibát
            var sut = CreateSut();
            var tampered = FlipByte(sut.Encrypt("hosszabb titkos érték"), fromStart: 13);

            Assert.Throws<InvalidOperationException>(() => sut.Decrypt(tampered));
        }

        [Fact]
        public void Decrypt_TamperedNonce_Throws()
        {
            var sut = CreateSut();
            var tampered = FlipByte(sut.Encrypt("titok"), fromStart: 0);

            Assert.Throws<InvalidOperationException>(() => sut.Decrypt(tampered));
        }

        [Fact]
        public void Decrypt_WithADifferentKey_Throws()
        {
            var ciphertext = CreateSut().Encrypt("titok");
            var otherKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            Assert.Throws<InvalidOperationException>(() => CreateSut(otherKey).Decrypt(ciphertext));
        }

        [Fact]
        public void Decrypt_NotBase64_ThrowsFormatException()
        {
            //Nem AppException: a hívó oldalon ez konfigurációs hiba, nem felhasználói input
            Assert.Throws<FormatException>(() => CreateSut().Decrypt("ez nem base64 @@@"));
        }

        /// <summary>
        /// Egy bájt legalsó bitjét fordítja meg a prefix mögötti base64 tartalomban.
        /// A fromEnd 1 a GCM tag utolsó bájtját jelenti.
        /// </summary>
        private static string FlipByte(string ciphertext, int fromStart = -1, int fromEnd = -1)
        {
            var data = Convert.FromBase64String(ciphertext[EncryptionSvc.Prefix.Length..]);
            var index = fromEnd >= 0 ? data.Length - fromEnd : fromStart;

            data[index] ^= 0x01;

            return EncryptionSvc.Prefix + Convert.ToBase64String(data);
        }
    }
}
