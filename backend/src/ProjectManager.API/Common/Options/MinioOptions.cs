namespace ProjectManager.API.Common.Options
{
    public class MinioOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public bool UseSSL { get; set; } = false;
        public string? PublicUrl { get; set; }
    }
}
