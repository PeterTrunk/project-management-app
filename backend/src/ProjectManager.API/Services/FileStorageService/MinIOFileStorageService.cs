using Minio;
using Minio.DataModel.Args;

namespace ProjectManager.API.Services.FileStorageService
{
    public class MinIOFileStorageService : IFileStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName;

        public MinIOFileStorageService()
        {
            var endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")
                ?? throw new InvalidOperationException("MINIO_ENDPOINT nincs beállítva!");
            var accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY")
                ?? throw new InvalidOperationException("MINIO_ACCESS_KEY nincs beállítva!");
            var secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY")
                ?? throw new InvalidOperationException("MINIO_SECRET_KEY nincs beállítva!");
            var useSSL = Environment.GetEnvironmentVariable("MINIO_USE_SSL") == "true";

            _bucketName = Environment.GetEnvironmentVariable("MINIO_BUCKET")
                ?? throw new InvalidOperationException("MINIO_BUCKET nincs beállítva!");

            _minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSSL)
                .Build();
        }

        public async Task<string> UploadFileAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string storageKey)
        {
            // Bucket létrehozása ha nem létezik
            var bucketExists = await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName));

            if (!bucketExists)
            {
                await _minioClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucketName));
            }

            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(storageKey)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType));

            return storageKey;
        }

        public async Task<Stream> GetFileStreamAsync(string storageKey)
        {
            var memoryStream = new MemoryStream();

            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(storageKey)
                .WithCallbackStream(async (stream, ct)=>
                {
                    await stream.CopyToAsync(memoryStream, ct);
                }));

            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task DeleteFileAsync(string storageKey)
        {
            await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(storageKey));
        }

        public string GenerateStorageKey(Guid projectId, Guid? taskId, string fileName)
        {
            var sanitizedFileName = Path.GetFileName(fileName);
            var fileId = Guid.NewGuid();

            if (taskId.HasValue)
            {
                return $"attachments/{projectId}/tasks/{taskId}/{fileId}_{sanitizedFileName}";
            }
            else
            {
                return $"attachments/{projectId}/shared/{fileId}_{sanitizedFileName}";
            }
        }
        
        public async Task StreamFileAsync(string storageKey, Stream destination, CancellationToken ct = default)
        {
            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(storageKey)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    await stream.CopyToAsync(destination, cancellationToken);
                }));
        }

        public async Task<string> GeneratePresignedPutUrlAsync(string storageKey, string contentType, int expirySeconds = 120)
        {
            var args = new PresignedPutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(storageKey)
                .WithExpiry(expirySeconds);

            var url = await _minioClient.PresignedPutObjectAsync(args);

            //Belső URL cseréje publikus URL-re
            var publicUrl = Environment.GetEnvironmentVariable("MINIO_PUBLIC_URL");
            if (!string.IsNullOrEmpty(publicUrl))
            {
                url = url.Replace("http://minio:9000", publicUrl);
            }

            return url;
        }

        public async Task<ObjectInfo?> GetObjectInfoAsync(string storageKey)
        {
            try
            {
                var stat = await _minioClient.StatObjectAsync(new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(storageKey));

                return new ObjectInfo
                {
                    Size = stat.Size,
                    ContentType = stat.ContentType
                };
            }
            catch
            {
                return null;
            }
        }
    }
}