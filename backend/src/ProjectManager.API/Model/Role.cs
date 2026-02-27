namespace ProjectManager.API.Model
{
    public class Role
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        // Egy role-hoz sok UserRole tartozhat
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}