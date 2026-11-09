using ProjectManager.API.DTOs.Comments;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Realtime
{
    /// <summary>
    /// A komment-események payloadjának szerződése.
    ///
    /// <b>Miért érdemel saját tesztet.</b> A kliens a <c>CommentAdded</c> payloadját
    /// KÖZVETLENÜL használja: a kommentek tárolója ugyanazt az alakot kapja az eseményből,
    /// mint a REST válaszból. Ez a megtakarítás alapja - korábban a kliens minden idegen
    /// komment érkezésekor újratöltötte a teljes listát, mert a payload mezőnevei eltértek
    /// a DTO-tól (<c>commentId</c>, <c>createdById</c>, <c>createdByName</c>), és az
    /// <c>UpdatedAt</c> is kimaradt.
    ///
    /// Egy mezőnév-elírás vagy egy kimaradt mező itt NÉMA hiba: a backend fordul, a teszt
    /// zöld, és a böngészőben egy üres szerzőnévvel megjelenő komment lesz belőle. Ezért a
    /// teszt nem egy MÁSOLATOT hasonlít össze, hanem magát a DTO-t: ha bármelyik oldal
    /// változik, itt bukik el, nem a felhasználónál.
    /// </summary>
    public class CommentEventPayloadTests : DatabaseTestBase
    {
        public CommentEventPayloadTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task CommentAdded_CarriesExactlyTheResponseDtoFields()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, ctx) = ServiceFactory.CreateCommentService(context, seed.Owner.Id);

            await sut.CommentOnTaskAsync(
                seed.Project.Id, seed.Task.Id, new CreateCommentDto { Body = "Egy megjegyzés" });

            var call = Assert.Single(ctx.Hub.CallsToProject(seed.Project.Id), c => c.Method == "CommentAdded");
            var payload = call.Arguments[0]!;

            //Kisbetűsítve hasonlítunk: a payload camelCase (a JSON alakja), a DTO PascalCase.
            //A szerződés a mezők KÉSZLETE, nem az írásmód - azt a szerializáló adja.
            static IEnumerable<string> FieldNames(Type type) => type.GetProperties()
                .Select(p => p.Name.ToLowerInvariant())
                .OrderBy(name => name);

            //Mindkét irány: a hiányzó mező üres értéket adna a felületen, a felesleges pedig
            //azt jelezné, hogy a két alak elkezdett szétcsúszni.
            Assert.Equal(FieldNames(typeof(CommentResponseDto)), FieldNames(payload.GetType()));
        }

        [Fact]
        public async Task CommentAdded_CarriesTheActualValues()
        {
            //A mezőnevek egyezése önmagában nem elég: egy helyes nevű, de üresen hagyott mező
            //ugyanúgy üres szerzőnevet adna a felületen.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, ctx) = ServiceFactory.CreateCommentService(context, seed.Owner.Id);

            var created = await sut.CommentOnTaskAsync(
                seed.Project.Id, seed.Task.Id, new CreateCommentDto { Body = "Egy megjegyzés" });

            var call = Assert.Single(ctx.Hub.CallsToProject(seed.Project.Id), c => c.Method == "CommentAdded");
            var payload = call.Arguments[0]!;

            object? Field(string name) => payload.GetType().GetProperty(name)?.GetValue(payload);

            Assert.Equal(created.Id, Field("id"));
            Assert.Equal(seed.Task.Id, Field("taskId"));
            Assert.Equal(seed.Owner.Id, Field("userId"));
            Assert.Equal(seed.Owner.DisplayName, Field("userName"));
            Assert.Equal("Egy megjegyzés", Field("body"));
            //Az UpdatedAt nem származtatás: a központi időbélyegzés állítja be, és a payload
            //innen veszi. A korábbi "a CreatedAt-tal egyezik, mert szerkesztés nincs" feltevés
            //az első szerkesztési funkcióval elromlott volna.
            Assert.NotNull(Field("updatedAt"));
        }
    }
}
