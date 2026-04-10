using System.Security.Claims;

namespace ProjectManager.API.Services.CurrentUserService
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid UserId => Guid.Parse(
            _httpContextAccessor.HttpContext!.User
                .FindFirstValue(ClaimTypes.NameIdentifier)!);

        public string Email => _httpContextAccessor.HttpContext!.User
            .FindFirstValue(ClaimTypes.Email) ?? "";

        public string DisplayName => _httpContextAccessor.HttpContext!.User
            .FindFirstValue(ClaimTypes.Name) ?? "";
    }
}
