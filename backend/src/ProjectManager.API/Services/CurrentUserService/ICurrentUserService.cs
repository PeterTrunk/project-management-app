namespace ProjectManager.API.Services.CurrentUserService
{
    public interface ICurrentUserService
    {
        Guid UserId { get; }
        string Email { get; }
        string DisplayName { get; }
    }
}
