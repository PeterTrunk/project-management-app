using FluentValidation;

namespace ProjectManager.Tests.Validators
{
    /// <summary>
    /// Hálót feszít a lefedettség alá: egy újonnan hozzáadott validátor tesztek nélkül
    /// nem maradhat észrevétlen. A névkonvenció XyzDtoValidator -> XyzDtoValidatorTests.
    /// </summary>
    public class ValidatorCoverageTests
    {
        private static IEnumerable<Type> DeclaredValidators() =>
            typeof(ProjectManager.API.Validators.ProjectValidators.CreateProjectDtoValidator).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract
                            && t.BaseType is { IsGenericType: true }
                            && t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>));

        [Fact]
        public void EveryValidator_HasATestClass()
        {
            var testClassNames = typeof(ValidatorCoverageTests).Assembly
                .GetTypes()
                .Select(t => t.Name)
                .ToHashSet();

            var untested = DeclaredValidators()
                .Select(t => t.Name)
                .Where(name => !testClassNames.Contains(name + "Tests"))
                .OrderBy(name => name)
                .ToList();

            Assert.True(
                untested.Count == 0,
                $"Tesztek nélküli validátorok: {string.Join(", ", untested)}");
        }

        //Ha ez a szám csökken, egy validátor eltűnt: a hozzá tartozó tesztfájl is törlendő
        [Fact]
        public void ValidatorCount_MatchesTheKnownSet()
        {
            Assert.Equal(34, DeclaredValidators().Count());
        }
    }
}
