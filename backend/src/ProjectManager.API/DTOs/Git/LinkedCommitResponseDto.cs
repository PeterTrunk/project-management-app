namespace ProjectManager.API.DTOs.Git
{
    public class LinkedCommitResponseDto
    {
        public Guid Id { get; set; }
        public string CommitSha { get; set; } = string.Empty;
        public string? CommitUrl { get; set; }
        public string Message { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CommittedAt { get; set; }

        //Igaz, ha ezt a hozzárendelést ember állította be, nem az illesztő.
        public bool IsManuallyLinked { get; set; }

        public Guid? TaskId { get; set; }
        public string? TaskKey { get; set; }
        public string? TaskTitle { get; set; }
    }
}
