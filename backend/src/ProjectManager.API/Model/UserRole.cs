namespace ProjectManager.API.Model
{
    public class UserRole
    {
        // FK mezők - ezek lesznek a composite PK
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public DateTime AssignedAt { get; set; }

        // Navigation properties - mindkét irányba mutat
        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }
}