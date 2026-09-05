namespace ProjectManager.API.Model
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public bool RememberMe { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        
        public User User { get; set; } = null!;
    }
}
