using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using ProjectManager.API.Common.Options;

namespace ProjectManager.API.Services.FileStorageService
{
    /// <summary>
    /// Fájltárolás S3-kompatibilis objektumtárolóban.
    ///
    /// Szándékosan NEM a szolgáltató nevét viseli. 
    /// A MinIO leváltásakor kiderült, hogy ez a kód egyetlen MinIO-specifikus elemet sem tartalmaz: 
    /// a Minio NuGet csomag szabványos S3 API-t beszél, tehát ugyanez az implementáció szolgálja ki a SeaweedFS-t is.
    /// Csak az endpoint és a kulcsok változnak, konfigurációból.
    /// </summary>
    public class S3FileStorageService : IFileStorageService
    {
        private readonly IMinioClient _storageClient;
        private readonly IMinioClient _presignedClient;
        private readonly string _bucketName;

        public S3FileStorageService(IOptions<ObjectStorageOptions> options)
        {
            var opt = options.Value;

            _bucketName = opt.Bucket;

            var presignedEndpoint = !string.IsNullOrEmpty(opt.PublicUrl)
                ? opt.PublicUrl.Replace("https://", "").Replace("http://", "")
                : opt.Endpoint;
            var presignedUseSSL = !string.IsNullOrEmpty(opt.PublicUrl)
                && opt.PublicUrl.StartsWith("https://");

            //Két kliens kell, és ez minden S3-tárolónál így van:
            //a SigV4 aláírás tartalmazza a Host fejlécet, ezért a presigned URL-t a PUBLIKUS címmel kell aláírni
            //- különben a böngészőből érkező kérés aláírása nem egyezne.
            //A belső műveletek közben a konténerhálózaton maradnak.
            //
            //1. Belső műveletek (upload, download, delete) - belső endpoint
            _storageClient = new MinioClient()
                .WithEndpoint(opt.Endpoint)
                .WithCredentials(opt.AccessKey, opt.SecretKey)
                .WithSSL(opt.UseSSL)
                .Build();

            //2. Presigned URL generálás - publikus endpoint
            _presignedClient = new MinioClient()
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
            var bucketExists = await _storageClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName));

            if (!bucketExists)
            {
                await _storageClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucketName));
            }

            await _storageClient.PutObjectAsync(new PutObjectArgs()
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

            await _storageClient.GetObjectAsync(new GetObjectArgs()
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
            await _storageClient.RemoveObjectAsync(new RemoveObjectArgs()
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
            await _storageClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(storageKey)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    await stream.CopyToAsync(destination, cancellationToken);
                }));
        }

        public async Task<string> GeneratePresignedPutUrlAsync(string storageKey, string contentType, int expirySeconds = 120)
        {
            //FIGYELEM - a WithHeaders itt NEM teszi az aláírásba a Content-Type-ot.
            //
            //Méréssel ellenőrizve a Minio SDK 7.0.0-val, MinIO és SeaweedFS ellen is:
            //a kiadott URL-ben "X-Amz-SignedHeaders=host" áll, a fejléc pedig egy query paraméterként szivárog ki,
            //a .NET típusnév értékével. Vagyis a tároló NEM utasítja el az eltérő
            //Content-Type-pal érkező PUT-ot - mindhárom eset (helyes, eltérő, hiányzó) 200-at ad.
            //
            //Ezért a fájltípust az AttachmentService confirm lépése kényszeríti ki: ott a
            //StatObject-ből olvasott tényleges típus össze van vetve a bejelentettel, és eltérés
            //esetén nem jön létre Attachment rekord. A hívás itt azért marad, mert a szándékot
            //kifejezi és költsége nincs - de védelemnek NEM tekinthető.
            var args = new PresignedPutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(storageKey)
                .WithHeaders(new Dictionary<string, string>
                {
                    ["Content-Type"] = contentType
                })
                .WithExpiry(expirySeconds);

            return await _presignedClient.PresignedPutObjectAsync(args);
        }

        public async Task<ObjectInfo?> GetObjectInfoAsync(string storageKey)
        {
            try
            {
                var stat = await _storageClient.StatObjectAsync(new StatObjectArgs()
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