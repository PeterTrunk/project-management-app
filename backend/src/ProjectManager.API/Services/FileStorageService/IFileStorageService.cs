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
    }
}
