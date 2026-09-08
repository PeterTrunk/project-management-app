using ProjectManager.API.Common.Security;

namespace ProjectManager.Tests.Common
{
    /// <summary>
    /// A generált tokenek jelszó-visszaállító és meghívó linkekbe kerülnek, tehát URL-ben
    /// utaznak: a base64 '+', '/' és '=' karaktereinek le kell cserélődniük.
    /// </summary>
    public class SecureTokenGeneratorTests
    {
        [Fact]
        public void Generate_ContainsNoUrlUnsafeCharacters()
        {
            //Egy futás nem elég: a '+' és '/' csak bizonyos bájtmintáknál keletkezik
            for (var i = 0; i < 200; i++)
            {
                var token = SecureTokenGenerator.Generate();
                Assert.DoesNotContain('+', token);
                Assert.DoesNotContain('/', token);
                Assert.DoesNotContain('=', token);
            }
        }

        [Fact]
        public void Generate_ProducesOnlyBase64UrlAlphabet()
        {
            var token = SecureTokenGenerator.Generate();
            Assert.All(token, c => Assert.True(
                char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_',
                $"Nem URL-biztos karakter a tokenben: '{c}'"));
        }

        [Fact]
        public void Generate_DefaultLength_Returns32BytesWorth()
        {
            //32 bájt base64-ben 44 karakter, amiből a lezáró '=' lekerül
            Assert.Equal(43, SecureTokenGenerator.Generate().Length);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(64)]
        public void Generate_CustomLength_DecodesBackToRequestedByteCount(int byteLength)
        {
            var token = SecureTokenGenerator.Generate(byteLength);

            //Vissza az eredeti base64 ábécére, a levágott padding pótlásával
            var base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

            Assert.Equal(byteLength, Convert.FromBase64String(base64).Length);
        }

        [Fact]
        public void Generate_ProducesUniqueTokens()
        {
            var tokens = Enumerable.Range(0, 1000)
                .Select(_ => SecureTokenGenerator.Generate())
                .ToHashSet();

            Assert.Equal(1000, tokens.Count);
        }
    }
}
