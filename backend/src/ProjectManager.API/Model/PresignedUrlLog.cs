namespace ProjectManager.API.Model
{
    public class PresignedUrlLog
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? TaskId { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Confirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public Guid CreatedById { get; set; }

        public Project Project { get; set; } = null!;
        public User CreatedBy { get; set; } = null!;
    }
}
