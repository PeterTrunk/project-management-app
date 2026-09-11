using Microsoft.AspNetCore.SignalR;
using ProjectManager.API.Hubs;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>Egy rögzített SignalR küldés.</summary>
    /// <param name="Target">Kinek ment: pl. <c>group:project-{id}</c> vagy <c>all</c>.</param>
    /// <param name="Method">Az esemény neve, pl. <c>TaskCreated</c>.</param>
    /// <param name="Arguments">A payload, ahogy a szolgáltatás átadta.</param>
    public sealed record HubCall(string Target, string Method, object?[] Arguments);

    /// <summary>
    /// Az IHubContext az egyetlen függőség, amihez valódi implementáció esetén futó SignalR
    /// szerver kellene - ezért kap duplát. Minden más (AppDbContext, Lexorank, Counter, ActivityService) valódi marad.
    ///
    /// Miért kézzel és nem mock könyvtárral: 
    /// A hívások egy sima listába gyűlnek, tehát az állítás egy hétköznapi LINQ kifejezés, nem egy könyvtár saját nyelvtana.
    /// Ráadásul a SignalR SendAsync valójában EXTENSION metódus az IClientProxy-n, amit mockolni nem is lehet
    /// mert csak az alatta lévő SendCoreAsync-et. 
    /// Itt ez a különbség nem okoz meglepetést, mert az extension is ide fut be.
    /// </summary>
    public sealed class RecordingHubContext : IHubContext<ProjectHub>
    {
        private readonly List<HubCall> _calls = new();

        public IReadOnlyList<HubCall> Calls => _calls;

        public IHubClients Clients { get; }
        public IGroupManager Groups { get; } = new NoOpGroupManager();

        public RecordingHubContext() => Clients = new RecordingClients(_calls);

        /// <summary>Az adott projekt csoportjába küldött események.</summary>
        public IEnumerable<HubCall> CallsToProject(Guid projectId) =>
            _calls.Where(c => c.Target == $"group:project-{projectId}");

        /// <summary>Elküldtük-e az adott eseményt a projekt csoportjába?</summary>
        public bool SentToProject(Guid projectId, string method) =>
            CallsToProject(projectId).Any(c => c.Method == method);

        public void Clear() => _calls.Clear();

        private sealed class RecordingClients : IHubClients
        {
            private readonly List<HubCall> _calls;

            public RecordingClients(List<HubCall> calls) => _calls = calls;

            private IClientProxy Proxy(string target) => new RecordingClientProxy(_calls, target);

            public IClientProxy All => Proxy("all");
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy("all-except");
            public IClientProxy Client(string connectionId) => Proxy($"client:{connectionId}");
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy("clients");
            public IClientProxy Group(string groupName) => Proxy($"group:{groupName}");
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy($"group-except:{groupName}");
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy("groups");
            public IClientProxy User(string userId) => Proxy($"user:{userId}");
            public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy("users");
        }

        private sealed class RecordingClientProxy : IClientProxy
        {
            private readonly List<HubCall> _calls;
            private readonly string _target;

            public RecordingClientProxy(List<HubCall> calls, string target)
            {
                _calls = calls;
                _target = target;
            }

            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                _calls.Add(new HubCall(_target, method, args));
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// A csoportkezelés a hubon történik, nem a szolgáltatásokban,
        /// ezért itt nincs mit rögzíteni. Ha valaha kell, ez a hely bővül.
        /// </summary>
        private sealed class NoOpGroupManager : IGroupManager
        {
            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
