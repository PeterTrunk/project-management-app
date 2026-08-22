namespace ProjectManager.API.DTOs.Attachment
{
    public class PresignedUrlResponseDto
    {
        public string PresignedUrl { get; set; } = string.Empty;
        public string StorageKey { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
