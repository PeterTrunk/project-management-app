using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// A bejelentkezett felhasználó a szolgáltatásokban az ICurrentUserService-en át érhető el,
    /// ami élesben a HttpContext claimjeiből olvas. Teszt közben nincs HTTP kérés, ezért kell
    /// egy dupla - de három property miatt nem éri meg mock könyvtár, egy osztály olcsóbb és
    /// olvashatóbb.
    /// </summary>
    public sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid userId, string displayName = "Teszt Elek", string email = "teszt@example.com")
        {
            UserId = userId;
            DisplayName = displayName;
            Email = email;
        }

        public Guid UserId { get; }
        public string Email { get; }
        public string DisplayName { get; }
    }
}
