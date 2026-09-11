using Microsoft.Extensions.Logging.Abstractions;
using ProjectManager.API.Data;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CounterService;
using ProjectManager.API.Services.LexorankService;
using ProjectManager.API.Services.ProjectTaskService;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Amit a szolgáltatás a teszt alatt kap, és amit a teszt utólag megvizsgálhat.
    /// </summary>
    public sealed record ServiceContext(AppDbContext Db, RecordingHubContext Hub);

    /// <summary>
    /// Szolgáltatásokat állít össze VALÓDI függőségekkel.
    ///
    /// Szándékosan nincs helyettesítve:
    /// - AppDbContext: konkrét osztály, és pont ez a lényeg
    /// - LexorankService: tiszta, 26 tesztje van; egy dupla érvénytelen Position-t adna, és elfedné a rendezési hibákat
    /// - CounterService: UGYANAZZAL a contexttel, mert Serializable tranzakciót nyit azon a kapcsolaton,
    ///   pont mint élesben. Egy 0-t adó dupla elfedné a TaskKey ütközéseket
    /// - ActivityService: valódi, így az állítás nem az, hogy "meghívták", hanem hogy a sor
    ///   tényleg bekerült az Activities táblába
    ///
    /// Egyedül az IHubContext kap duplát (RecordingHubContext), mert valódi implementációhoz
    /// futó SignalR szerver kellene.
    /// </summary>
    public static class ServiceFactory
    {
        public static (TaskService Sut, ServiceContext Ctx) CreateTaskService(
            AppDbContext context,
            Guid currentUserId,
            string currentUserDisplayName = "Teszt Elek")
        {
            var currentUser = new FakeCurrentUserService(currentUserId, currentUserDisplayName);
            var hub = new RecordingHubContext();
            var activityService = new ActivityService(context, currentUser);

            var sut = new TaskService(
                context,
                new LexorankService(),
                currentUser,
                hub,
                activityService,
                new CounterService(context),
                NullLogger<TaskService>.Instance);

            return (sut, new ServiceContext(context, hub));
        }
    }
}
