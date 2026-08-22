using Minio.DataModel.Args;

namespace ProjectManager.API.Services.FileStorageService
{
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string storageKey
        );
        Task<Stream> GetFileStreamAsync(string storageKey);
        Task DeleteFileAsync(string storageKey);
        string GenerateStorageKey(Guid projectId, Guid? taskId, string fileName);
        Task StreamFileAsync(string storageKey, Stream destination, CancellationToken ct = default);
        Task<string> GeneratePresignedPutUrlAsync(string storageKey, string contentType, int expirySeconds = 120);
        Task<ObjectInfo?> GetObjectInfoAsync(string storageKey);
    }
}
