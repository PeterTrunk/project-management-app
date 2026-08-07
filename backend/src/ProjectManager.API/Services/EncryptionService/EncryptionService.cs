using System.Security.Cryptography;
using System.Text;

namespace ProjectManager.API.Services.EncryptionService
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        public EncryptionService(string base64Key)
        {
            _key = Convert.FromBase64String(base64Key);
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

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string base64Ciphertext)
        {
            var data = Convert.FromBase64String(base64Ciphertext);

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
