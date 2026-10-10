namespace ProjectManager.API.Common.Options
{
    /// <summary>
    /// Egy S3-kompatibilis objektumtárolo eléréséhez szükséges beállítások.
    /// Szándékosan szolgáltató-független: 
    /// a MinIO leváltásakor derült ki, hogy a provider nevét tartalmazó típus- 
    /// és változónevek önmagukban is karbantartási terhet jelentenek, miközben a konfiguráció minden S3-tárolónál azonos.
    /// </summary>
    public class ObjectStorageOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public bool UseSSL { get; set; } = false;
        public string? PublicUrl { get; set; }
    }
}
