namespace ProjectManager.API.DTOs.Attachment
{
    public class PresignedUrlRequestDto
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }
}
