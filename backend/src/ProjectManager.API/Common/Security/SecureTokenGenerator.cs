using System.Security.Cryptography;

namespace ProjectManager.API.Common.Security
{
    /// <summary>
    /// Kriptográfiai minőségű, URL-biztos tokenek előállítása.
    /// A Guid.NewGuid() a gyakorlatban ma CSPRNG-ből jön, de a dokumentáció ezt nem
    /// garantálja - biztonsági tokennél ne erre támaszkodjunk.
    /// </summary>
    public static class SecureTokenGenerator
    {
        public static string Generate(int byteLength = 32)
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}
