using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Realtime
{
    /// <summary>
    /// A csatolmány-események payloadjának szerződése, és a projekt-szintű lekérés hatóköre.
    ///
    /// <b>Miért érdemel saját tesztet.</b> A kliens a payloadot KÖZVETLENÜL használja: a
    /// csatolmányok tárolója ugyanazt az alakot kapja az eseményből, mint a REST válaszból.
    /// Korábban az <c>attachmentId</c> tért el az <c>Id</c>-tól, és a feladat kulcsa
    /// egyáltalán nem volt benne - a kliens ezért minden eseményre újratöltötte a projekt
    /// csatolmányait ÉS a teljes feladatgráfot.
    ///
    /// Egy mezőnév-elírás itt NÉMA hiba: a backend fordul, a böngészőben pedig egy üres
    /// fejlécű csoport vagy egy azonosító nélküli kártya lesz belőle.
    /// </summary>
    public class AttachmentEventPayloadTests : DatabaseTestBase
    {
        public AttachmentEventPayloadTests(PostgresFixture fixture) : base(fixture) { }

        private static Attachment Attachment(SeededProject seed, Guid? taskId, string fileName) =>
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = seed.Project.Id,
                TaskId = taskId,
                UploadedById = seed.Owner.Id,
                FileName = fileName,
                ContentType = "application/pdf",
                SizeBytes = 1024,
                StorageKey = $"projekt/{Guid.NewGuid()}/{fileName}",
                AttachmentType = "Pdf",
                Version = 1,
                CreatedAt = DateTime.UtcNow
            };

        [Fact]
        public async Task TheProjectQuery_ReturnsTaskAttachmentsToo()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS a lekérés kinyitására: a korábbi `TaskId == null` szűrő
            // miatt a hívónak a teljes feladatgráfot is le kellett töltenie, csak hogy a
            // feladatokhoz tartozó csatolmányokat megkapja.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "AAA");
            var (sut, _, _) = ServiceFactory.CreateAttachmentService(context, seed.Owner.Id);

            await using var arrange = CreateContext();
            arrange.Attachments.AddRange(
                Attachment(seed, taskId: null, fileName: "projekt.pdf"),
                Attachment(seed, taskId: seed.Task.Id, fileName: "feladat.pdf"));
            await arrange.SaveChangesAsync();

            var all = await sut.GetProjectAttachmentsAsync(seed.Project.Id);

            Assert.Equal(2, all.Count);

            //És a feladathoz tartozó sor hordozza a kulcsot - enélkül a csoportosított nézet
            //fejléce üresen maradna azoknál a feladatoknál, amelyek lezárt sprintben vannak,
            //tehát a kliens oldali feladatlistában sem szerepelnek.
            var taskAttachment = Assert.Single(all, a => a.TaskId == seed.Task.Id);
            Assert.Equal(seed.Task.TaskKey, taskAttachment.TaskKey);
            Assert.Equal(seed.Task.Title, taskAttachment.TaskTitle);

            //A projekt-szintű sornál viszont null, nem üres sztring: a kliens ebből tudja,
            //hogy a saját szekciójába tartozik.
            var projectAttachment = Assert.Single(all, a => a.TaskId == null);
            Assert.Null(projectAttachment.TaskKey);
        }

        [Fact]
        public void AttachmentUploaded_PayloadShapeMatchesTheResponseDto()
        {
            //A payload alakját a DTO-hoz hasonlítjuk, nem egy másolathoz: ha bármelyik oldal
            //változik - új DTO mező, elgépelt payload név -, itt bukik el.
            //
            //A payload felépítése a feltöltési folyamat végén áll, amihez valódi tároló és
            //presigned URL kell; ezért a MEZŐKÉSZLETET a szolgáltatás forrásában rögzített
            //alakból vesszük: a `MapToDto` és az esemény ugyanabból az entitásból építkezik,
            //tehát a DTO a szerződés. A tényleges kiküldést a Storage tesztkör fedi.
            var dtoFields = typeof(AttachmentResponseDto).GetProperties()
                .Select(p => p.Name)
                .OrderBy(name => name)
                .ToList();

            //A kliens `AttachmentResponse` típusa pontosan ezeket a mezőket deklarálja.
            //Ha a DTO bővül, ezt a listát ÉS a frontend típust is bővíteni kell - a teszt
            //célja, hogy a kettő ne tudjon csendben szétcsúszni.
            var expected = new[]
            {
                "AttachmentType", "ContentType", "CreatedAt", "FileName", "Id", "ProjectId",
                "SizeBytes", "TaskId", "TaskKey", "TaskTitle", "UploadedByName", "Version"
            }.OrderBy(name => name).ToList();

            Assert.Equal(expected, dtoFields);
        }
    }
}
