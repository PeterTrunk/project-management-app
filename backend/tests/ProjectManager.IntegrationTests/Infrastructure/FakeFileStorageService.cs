using ProjectManager.API.Services.FileStorageService;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Kézi dupla a tárolóhoz, a <c>RecordingHubContext</c> mintájára.
    /// </summary>
    /// <remarks>
    /// Azért dupla és nem valódi tároló, mert a vizsgált viselkedés az ADATBÁZISBAN van: a
    /// megerősítés igénylése és annak visszavonása. Egy valódi tárolókonténer ezt nem
    /// mérné pontosabban, viszont minden tesztet megdrágítana - a tároló tényleges
    /// viselkedését (aláírt típus- és méretkötés) külön tesztosztály méri, saját
    /// konténerrel.
    ///
    /// A törléseket listába gyűjti, mert a takarító feladat viselkedéséhez az a kérdés,
    /// hogy egy adott kulcsot elvitt-e.
    /// </remarks>
    public sealed class FakeFileStorageService : IFileStorageService
    {
        /// <summary>A "tárolóban lévő" objektumok: kulcs -> méret és típus.</summary>
        public Dictionary<string, ObjectInfo> Objects { get; } = new();

        public List<string> DeletedKeys { get; } = new();

        /// <summary>Felvesz egy objektumot úgy, ahogy egy sikeres feltöltés után látszana.</summary>
        public void PutObject(string storageKey, string contentType, long sizeBytes)
            => Objects[storageKey] = new ObjectInfo { Size = sizeBytes, ContentType = contentType };

        public Task<ObjectInfo?> GetObjectInfoAsync(string storageKey)
            => Task.FromResult(Objects.TryGetValue(storageKey, out var info) ? info : null);

        public Task DeleteFileAsync(string storageKey)
        {
            DeletedKeys.Add(storageKey);
            Objects.Remove(storageKey);
            return Task.CompletedTask;
        }

        /// <summary>
        /// A valódi implementáció alakját követi, az EGYEDI azonosítóval együtt.
        /// </summary>
        /// <remarks>
        /// Az azonosító nem díszítés: a `PresignedUrlLogs.StorageKey` egyedi indexen van,
        /// tehát két feltöltés ugyanazzal a fájlnévvel csak akkor vehető fel, ha a kulcs
        /// eltér. Az első, determinisztikus változatom emiatt buktatta el a párhuzamos
        /// tesztet - a dupla szerződésének itt a valódival egyeznie kell.
        /// </remarks>
        public string GenerateStorageKey(Guid projectId, Guid? taskId, string fileName)
            => taskId.HasValue
                ? $"attachments/{projectId}/tasks/{taskId}/{Guid.NewGuid()}_{fileName}"
                : $"attachments/{projectId}/shared/{Guid.NewGuid()}_{fileName}";

        public Task<PresignedUpload> GeneratePresignedUploadAsync(
            string storageKey, string contentType, long sizeBytes, int expirySeconds = 120)
            => Task.FromResult(new PresignedUpload
            {
                Url = $"https://tarolo.teszt/{storageKey}",
                Fields = new Dictionary<string, string> { ["key"] = storageKey }
            });

        public Task<string> UploadFileAsync(
            Stream fileStream, string fileName, string contentType, string storageKey)
        {
            PutObject(storageKey, contentType, fileStream.CanSeek ? fileStream.Length : 0);
            return Task.FromResult(storageKey);
        }

        public Task<Stream> GetFileStreamAsync(string storageKey)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task StreamFileAsync(string storageKey, Stream destination, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
