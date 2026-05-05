using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.DTOs.Git;
using ProjectManager.API.DTOs.Shared;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class TaskResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? BoardId { get; set; }
        public Guid? ColumnId { get; set; }
        public Guid? SprintId { get; set; }
        public List<string> AssigneeIds { get; set; } = new List<string>();
        public List<string> LabelIds { get; set; } = new List<string>();
        public List<CommitLinkResponseDto> CommitLinks { get; set; } = new List<CommitLinkResponseDto>();
        public List<PrLinkResponseDto> PrLinks { get; set; } = new List<PrLinkResponseDto>();
        public List<AttachmentResponseDto> Attachments { get; set; } = new List<AttachmentResponseDto>();
        public string CreatedByName { get; set; } = string.Empty;
        public string TaskKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Priority { get; set; }
        public string Position { get; set; } = string.Empty;
        public int? EstimateInMinutes { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
