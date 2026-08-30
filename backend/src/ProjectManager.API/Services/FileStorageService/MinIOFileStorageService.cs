using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using OtpNet;
using ProjectManager.API.Common.Options;

namespace ProjectManager.API.Services.FileStorageService
{
    public class MinIOFileStorageService : IFileStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly IMinioClient _presignedMinioClient;
        private readonly string _bucketName;

        public MinIOFileStorageService(IOptions<MinioOptions> options)
        {
            var opt = options.Value;

            _bucketName = opt.Bucket;

            var presignedEndpoint = !string.IsNullOrEmpty(opt.PublicUrl)
                ? opt.PublicUrl.Replace("https://", "").Replace("http://", "")
                : opt.Endpoint;
            var presignedUseSSL = !string.IsNullOrEmpty(opt.PublicUrl)
                && opt.PublicUrl.StartsWith("https://");

            //Két kliens:
            //1. Belső műveletek (upload, download, delete) ez lesz a belső endpoint
            _minioClient = new MinioClient()
                .WithEndpoint(opt.Endpoint)
                .WithCredentials(opt.AccessKey, opt.SecretKey)
                .WithSSL(opt.UseSSL)
                .Build();

            //2. Presigned URL generálás ez lesz a publikus endpoint
            _presignedMinioClient = new MinioClient()
                .WithEndpoint(presignedEndpoint)
                .WithCredentials(opt.AccessKey, opt.SecretKey)
                .WithSSL(presignedUseSSL)
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

            return await _presignedMinioClient.PresignedPutObjectAsync(args);
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