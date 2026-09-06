using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Options;
using System.Security.Cryptography;
using System.Text;

namespace ProjectManager.API.Services.EncryptionService
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        //Explicit jelölés: enélkül a "titkosított-e már?" kérdésre csak a Decrypt sikeressége válaszolna,
        //ami rossz kulcs esetén is hibát dob - és a hívó tévesen plain textnek minősítené a helyesen titkosított adatot.
        public const string Prefix = "enc:v1:";

        /// <summary>
        /// Igaz, ha az érték biztosan a mi titkosított formátumunk. A prefix nélküli (régi) ciphertextekre hamisat ad, 
        /// amelyeket a Decrypt továbbra is kezel.
        /// </summary>
        public static bool IsEncrypted(string? value) =>
            value != null && value.StartsWith(Prefix, StringComparison.Ordinal);

        public EncryptionService(IOptions<EncryptionOptions> options)
        {
            _key = Convert.FromBase64String(options.Value.Key);
            if (_key.Length != 32)
                throw new InvalidOperationException(
                    $"ENCRYPTION_KEY must be exactly 32 bytes, got {_key.Length}");
        }

        public string Encrypt(string plaintext)
        {
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // nonce || ciphertext || tag
            var result = new byte[NonceSize + ciphertext.Length + TagSize];
            nonce.CopyTo(result, 0);
            ciphertext.CopyTo(result, NonceSize);
            tag.CopyTo(result, NonceSize + ciphertext.Length);

            return Prefix + Convert.ToBase64String(result);
        }

        public string Decrypt(string base64Ciphertext)
        {
            //A prefix nélküli értékek a jelölés bevezetése előtt keletkeztek
            var payload = base64Ciphertext.StartsWith(Prefix, StringComparison.Ordinal)
                ? base64Ciphertext[Prefix.Length..]
                : base64Ciphertext;

            var data = Convert.FromBase64String(payload);

            var nonce = data[..NonceSize];
            var tag = data[^TagSize..];
            var ciphertext = data[NonceSize..^TagSize];

            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, TagSize);
            try
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "Decryption failed: invalid key or corrupted data.", ex);
            }

            return Encoding.UTF8.GetString(plaintext);
        }
    }
}
