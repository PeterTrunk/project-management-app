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

        /// <summary>
        /// A fenti háló VAKFOLTJÁT zárja be: csak a metódus <b>paramétereit</b> vizsgálta,
        /// a kérés <b>törzsét</b> nem. A projekt-hatókör viszont a törzsben is megérkezhet -
        /// és ott nem volt ellenőrizve.
        ///
        /// A szabály pontosan az, ami a hibaalak volt: <b>egy DTO nem ismételheti meg azt az
        /// azonosítót, amit a metódus már paraméterként megkap.</b> Ilyenkor ugyanaz az érték
        /// kétszer érkezik - egyszer az útvonalról (amire a jogosultság szól), egyszer a
        /// törzsből (amire semmi) -, és a kód a rosszat használhatja.
        ///
        /// Ez nem elméleti: a `CreateSprintDto.ProjectId`, a `CreateBoardDto.ProjectId` és a
        /// `CreateColumnDto.BoardId` mezőket a létrehozó metódusok a route értéke HELYETT
        /// használták. A jogosultságot a `ProjectRoleHandler` a route `projectId`-jére
        /// ellenőrzi, tehát a törzsből átvett azonosító átlépte a jogosultsági kaput:
        /// az "A" projekt adminja a "B" projektbe tudott írni.
        ///
        /// A `ColumnService` esete a legtanulságosabb: a route `boardId`-t helyesen
        /// ellenőrizte, majd a validált érték helyett a törzsből jövőt használta - ezért a
        /// `CreateColumnAsync_ForeignBoard_ThrowsNotFound` teszt ZÖLD volt a hiba mellett.
        ///
        /// Amit ez a teszt szándékosan NEM jelez: a `CreateTaskDto.BoardId`-hez hasonló
        /// mezőket. Ott a board a művelet CÉLJA, nem a hatóköre - az útvonal nem hordozza -,
        /// és a `TaskService` ellenőrzi, hogy a megadott board a projekthez tartozik.
        /// </summary>
        [Fact]
        public void NoScopedDto_RepeatsAnIdentifierTheMethodAlreadyReceives()
        {
            var offenders = new List<string>();

            foreach (var method in ScopedServices.SelectMany(t => t.GetMethods()))
            {
                var parameters = method.GetParameters();

                //Az azonositok, amiket a metodus mar megkap (tehat az utvonalrol jonnek)
                var fromRoute = parameters
                    .Where(p => p.ParameterType == typeof(Guid) && p.Name != null)
                    .Select(p => char.ToUpperInvariant(p.Name![0]) + p.Name![1..])
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var dtoType in parameters
                             .Select(p => p.ParameterType)
                             .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal)))
                {
                    foreach (var prop in dtoType.GetProperties().Where(pr => fromRoute.Contains(pr.Name)))
                        offenders.Add($"{method.Name}: {dtoType.Name}.{prop.Name}");
                }
            }

            offenders = offenders.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.True(
                offenders.Count == 0,
                "A metódus már megkapja ezt az azonosítót, a törzs mégis megadhatja: "
                + string.Join(", ", offenders)
                + ". Vedd ki a mezőt a DTO-ból, és a szolgáltatás a paraméterként kapott értéket használja.");
        }
    }
}
