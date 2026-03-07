using System.Numerics;

namespace ProjectManager.API.DTOs.Shared
{
    public class AttachmentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
    }
}
