namespace ProjectManager.API.Model
{
    public class Attachment
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? TaskId { get; set; }
        public Guid UploadedById { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public string AttachmentType {  get; set; } = string.Empty;
        public int Version { get; set; } = 0;
        public DateTime CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ProjectTask? ProjectTask { get; set; }  // nullable nav prop!
        public User UploadedBy { get; set; } = null!;
    }
}
