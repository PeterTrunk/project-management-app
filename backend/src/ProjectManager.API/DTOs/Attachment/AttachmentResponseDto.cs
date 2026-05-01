namespace ProjectManager.API.DTOs.Attachment
{
    public class AttachmentResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? TaskId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string AttachmentType { get; set; } = string.Empty;
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
