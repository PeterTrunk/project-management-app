using ProjectManager.API.Services.BoardService;
using ProjectManager.API.Services.ColumnService;
using ProjectManager.API.Services.CommentService;
using ProjectManager.API.Services.LabelService;
using ProjectManager.API.Services.ProjectTaskService;
using ProjectManager.API.Services.SprintService;
using System.Reflection;

namespace ProjectManager.IntegrationTests.CrossProject
{
    /// <summary>
    /// Hálót feszít a projekt-scoping tesztek alá: 
    /// Egy ÚJONNAN hozzáadott, projekt-hatókörű metódus nem maradhat teszt nélkül.
    ///
    /// Enélkül a lefedettség egy emlékezet kérdése lenne - és pont egy elfelejtett új metódus az, amin egy IDOR hiba bejön.
    /// 
    /// A névkonvenció: a teszt neve a metódus nevével kezdődik, aláhúzással folytatva,
    /// például <c>DeleteTaskAsync_ForeignTask_ThrowsNotFound</c>.
    /// </summary>
    public class CrossProjectCoverageTests
    {
        /// <summary>
        /// A mag 6 szolgáltatás. 
        /// A maradék hat (Attachment, Integration, Team, Git, GitWebhook, Statistics) tudatosan marad ki,
        /// a később olcsón hozzá lehet adni bármikor.
        /// </summary>
        private static readonly Type[] ScopedServices =
        [
            typeof(ITaskService),
            typeof(ISprintService),
            typeof(IColumnService),
            typeof(IBoardService),
            typeof(ICommentService),
            typeof(ILabelService)
        ];

        /// <summary>
        /// Azok a metódusok, amiknél a projekt-hatókör egyáltalán értelmezhető: 
        /// Az első paraméter a projectId, és van legalább még egy NEM nullozható Guid
        /// vagyis egy konkrét entitás azonosítója, ami elvben másik projekthez is tartozhat.
        ///
        /// A nullozható Guid paraméterek (pl. GetTasksAsync boardId szűrője) szándékosan kimaradnak:
        /// azok szűrők, nem kikeresett entitások - idegen érték esetén üres eredményt adnak, nem szivárogtatnak.
        /// </summary>
        private static IEnumerable<MethodInfo> ScopedMethods() =>
            ScopedServices
                .SelectMany(t => t.GetMethods())
                .Where(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length >= 2
                        && parameters[0].ParameterType == typeof(Guid)
                        && parameters[0].Name == "projectId"
                        && parameters.Skip(1).Any(p => p.ParameterType == typeof(Guid));
                });

        [Fact]
        public void EveryProjectScopedMethod_HasACrossProjectTest()
        {
            var testMethodNames = typeof(CrossProjectCoverageTests).Assembly
                .GetTypes()
                .Where(t => t.Namespace == typeof(CrossProjectCoverageTests).Namespace)
                .SelectMany(t => t.GetMethods())
                .Select(m => m.Name)
                .ToList();

            var untested = ScopedMethods()
                .Select(m => m.Name)
                .Distinct()
                .Where(name => !testMethodNames.Any(t => t.StartsWith(name + "_", StringComparison.Ordinal)))
                .OrderBy(name => name)
                .ToList();

            Assert.True(
                untested.Count == 0,
                "Projekt-hatókörű metódusok teszt nélkül: " + string.Join(", ", untested));
        }

        //Ha ez a szám csökken, egy metódus eltűnt: a hozzá tartozó teszt is törlendő.
        //Ha nő, az új metódus a fenti teszten úgyis fennakad, amíg nincs tesztje.
        [Fact]
        public void ScopedMethodCount_MatchesTheKnownSet()
        {
            Assert.Equal(28, ScopedMethods().Count());
        }
    }
}
