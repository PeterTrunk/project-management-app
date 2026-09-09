using Microsoft.AspNetCore.Http;
using ProjectManager.API.Common.Exceptions;

namespace ProjectManager.Tests.Common
{
    /// <summary>
    /// Az AppException leszármazottak státuszkódja a HTTP szerződés része: a frontend ezekre
    /// ágazik el (401/403 kijelentkeztet, 409 újratölt, 429 visszaszámol). Egy elcsúszott
    /// kód némán rossz felhasználói viselkedést okozna.
    /// </summary>
    public class AppExceptionTests
    {
        public static TheoryData<Type, int> ExpectedStatusCodes => new()
        {
            { typeof(ValidationException), StatusCodes.Status400BadRequest },
            { typeof(ForbiddenException), StatusCodes.Status403Forbidden },
            { typeof(NotFoundException), StatusCodes.Status404NotFound },
            { typeof(ConflictException), StatusCodes.Status409Conflict },
            { typeof(RateLimitException), StatusCodes.Status429TooManyRequests }
        };

        [Theory]
        [MemberData(nameof(ExpectedStatusCodes))]
        public void StatusCode_MatchesTheHttpContract(Type exceptionType, int expected)
        {
            var ex = (AppException)Activator.CreateInstance(exceptionType, "teszt üzenet")!;
            Assert.Equal(expected, ex.StatusCode);
        }

        [Theory]
        [MemberData(nameof(ExpectedStatusCodes))]
        public void Message_IsPreserved(Type exceptionType, int _)
        {
            var ex = (AppException)Activator.CreateInstance(exceptionType, "teszt üzenet")!;
            Assert.Equal("teszt üzenet", ex.Message);
        }

        //Ez a teszt akkor bukik el, ha valaki új AppException leszármazottat vezet be:
        //így nem lehet elfelejteni a státuszkód rögzítését.
        [Fact]
        public void EveryConcreteAppException_IsCovered()
        {
            var covered = ExpectedStatusCodes
                .Select(row => ((Type)row[0]!).Name)
                .OrderBy(name => name);

            var declared = typeof(AppException).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(AppException)) && !t.IsAbstract)
                .Select(t => t.Name)
                .OrderBy(name => name);

            Assert.Equal(declared, covered);
        }

        [Fact]
        public void AppException_IsAbstract()
        {
            //A middleware "kimehet a felhasználónak" döntése az AppException öröklésén múlik,
            //ezért az ősosztályt közvetlenül ne lehessen példányosítani.
            Assert.True(typeof(AppException).IsAbstract);
        }
    }
}
