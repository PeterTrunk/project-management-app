using Microsoft.Extensions.Logging.Abstractions;
using ProjectManager.API.Data;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.BoardService;
using ProjectManager.API.Services.ColumnService;
using ProjectManager.API.Services.CommentService;
using ProjectManager.API.Services.CounterService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.LabelService;
using ProjectManager.API.Services.LexorankService;
using ProjectManager.API.Services.ProjectTaskService;
using ProjectManager.API.Services.SprintService;

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
    /// - LexorankService: tiszta, 26 tesztje van; egy dupla érvénytelen Position-t adna,
    ///   és elfedné a rendezési hibákat
    /// - CounterService: UGYANAZZAL a contexttel, mert Serializable tranzakciót nyit azon a
    ///   kapcsolaton - pont mint élesben. Egy 0-t adó dupla elfedné a TaskKey ütközéseket
    /// - ActivityService: valódi, így az állítás nem az, hogy "meghívták", hanem hogy a sor
    ///   tényleg bekerült az Activities táblába
    ///
    /// Egyedül az IHubContext kap duplát (RecordingHubContext), mert valódi implementációhoz
    /// futó SignalR szerver kellene.
    /// </summary>
    public static class ServiceFactory
    {
        private static (FakeCurrentUserService User, RecordingHubContext Hub, ActivityService Activity) Common(
            AppDbContext context, Guid currentUserId, string displayName)
        {
            var user = new FakeCurrentUserService(currentUserId, displayName);
            return (user, new RecordingHubContext(), new ActivityService(context, user));
        }

        public static (TaskService Sut, ServiceContext Ctx) CreateTaskService(
            AppDbContext context, Guid currentUserId, string currentUserDisplayName = "Teszt Elek")
        {
            var (user, hub, activity) = Common(context, currentUserId, currentUserDisplayName);

            var sut = new TaskService(
                context,
                new LexorankService(),
                user,
                hub,
                activity,
                new CounterService(context),
                NullLogger<TaskService>.Instance);

            return (sut, new ServiceContext(context, hub));
        }

        public static (SprintService Sut, ServiceContext Ctx) CreateSprintService(
            AppDbContext context, Guid currentUserId, string currentUserDisplayName = "Teszt Elek")
        {
            var (user, hub, activity) = Common(context, currentUserId, currentUserDisplayName);

            var sut = new SprintService(
                context,
                new LexorankService(),
                hub,
                user,
                activity,
                NullLogger<SprintService>.Instance);

            return (sut, new ServiceContext(context, hub));
        }

        public static (ColumnService Sut, ServiceContext Ctx) CreateColumnService(
            AppDbContext context, Guid currentUserId, string currentUserDisplayName = "Teszt Elek")
        {
            var (user, hub, activity) = Common(context, currentUserId, currentUserDisplayName);

            var sut = new ColumnService(
                context, hub, user, activity, NullLogger<ColumnService>.Instance);

            return (sut, new ServiceContext(context, hub));
        }

        public static (BoardService Sut, ServiceContext Ctx) CreateBoardService(
            AppDbContext context, Guid currentUserId, string currentUserDisplayName = "Teszt Elek")
        {
            var (user, hub, activity) = Common(context, currentUserId, currentUserDisplayName);

            var sut = new BoardService(
                context, hub, user, activity, NullLogger<BoardService>.Instance);

            return (sut, new ServiceContext(context, hub));
        }

        public static (CommentService Sut, ServiceContext Ctx) CreateCommentService(
            AppDbContext context, Guid currentUserId, string currentUserDisplayName = "Teszt Elek")
        {
            var (user, hub, activity) = Common(context, currentUserId, currentUserDisplayName);

            var sut = new CommentService(
                context, user, hub, activity, NullLogger<CommentService>.Instance);

            return (sut, new ServiceContext(context, hub));
        }

        public static (LabelService Sut, ServiceContext Ctx) CreateLabelService(
            AppDbContext context, Guid currentUserId, string currentUserDisplayName = "Teszt Elek")
        {
            var (_, hub, _) = Common(context, currentUserId, currentUserDisplayName);

            //A LabelService nem naplóz és nem ismeri a bejelentkezett felhasználót
            var sut = new LabelService(context, hub, NullLogger<LabelService>.Instance);

            return (sut, new ServiceContext(context, hub));
        }
    }
}
