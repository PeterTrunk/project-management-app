using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Options;
using ProjectManager.API.Services.FileStorageService;

namespace ProjectManager.IntegrationTests.Storage;

/// <summary>
/// Az alairt feltoltesi engedely KIKENYSZERITESE a tarolo viselkedesen nyugszik, nem a mi
/// kodunkon: a POST policy feltetellistajat a tarolo ellenorzi. Egy tarolocsere vagy
/// verziovaltas ezt NEMAN elronthatja - ezert meri ez a teszt valodi konteneren, hogy
/// az eltero tipusu es az eltero meretu feltoltes tenylegesen elbukik.
///
/// Sajat kontenert indit (nem a Postgres fixture-t hasznalja), mert adatbazis nem kell hozza.
/// </summary>
public class PresignedUploadPolicyTests : IAsyncLifetime
{
    private const string Bucket = "attachments-test";
    private const string AccessKey = "testaccess";
    private const string SecretKey = "testsecret123";

    private IContainer _storage = null!;
    private S3FileStorageService _sut = null!;
    private readonly HttpClient _http = new();

    public async Task InitializeAsync()
    {
        //A keszenletet nem varo strategiara bizzuk: a port megnyilasa utan az S3 API meg par
        //pillanatig nem szolgal ki, ezert a tenyleges keszenletet lentebb egy ujraprobalo kor meri.
        _storage = new ContainerBuilder("chrislusf/seaweedfs:4.46")
            .WithEnvironment("AWS_ACCESS_KEY_ID", AccessKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", SecretKey)
            .WithCommand("mini", "-dir=/data", "-s3", "-s3.autoCreateBucket")
            .WithPortBinding(8333, true)
            .Build();

        await _storage.StartAsync();

        _sut = new S3FileStorageService(Options.Create(new ObjectStorageOptions
        {
            Endpoint = $"{_storage.Hostname}:{_storage.GetMappedPublicPort(8333)}",
            AccessKey = AccessKey,
            SecretKey = SecretKey,
            UseSSL = false,
            Bucket = Bucket,
            PublicUrl = ""
        }));

        //Az S3 API par pillanattal kesobb all fel, mint ahogy a port nyitva van.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                await _sut.UploadFileAsync(new MemoryStream(new byte[1]), "warmup", "text/plain", "warmup/probe");
                return;
            }
            catch
            {
                await Task.Delay(250);
            }
        }

        throw new InvalidOperationException("A tarolo nem valt elerhetove.");
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _storage.DisposeAsync();
    }

    private async Task<int> UploadAsync(string key, string declaredType, string sentType, long declaredSize, int actualBytes)
    {
        var upload = await _sut.GeneratePresignedUploadAsync(key, declaredType, declaredSize);

        using var form = new MultipartFormDataContent();
        foreach (var field in upload.Fields)
            form.Add(new StringContent(field.Value), field.Key);

        //A policy a Content-Type-ot urlapmezoként koti, nem fejlecként.
        form.Add(new StringContent(sentType), "Content-Type");

        //A file mezo kotelezoen az utolso.
        form.Add(new ByteArrayContent(new byte[actualBytes]), "file", "f.bin");

        var response = await _http.PostAsync(upload.Url, form);
        return (int)response.StatusCode;
    }

    [Fact]
    public async Task EgyezoTipusEsMeret_Sikeres()
    {
        var status = await UploadAsync("attachments/p/ok.png", "image/png", "image/png", 64, 64);

        Assert.InRange(status, 200, 299);
    }

    [Fact]
    public async Task ElteroTipus_ATaroloElutasitja()
    {
        //Ez a lenyegi allitas: a bejelentett kep helyett futtathato allomanyt kuldunk.
        var status = await UploadAsync("attachments/p/baditype.png", "image/png", "application/x-msdownload", 64, 64);

        Assert.Equal(403, status);
    }

    [Fact]
    public async Task ABejelentettnelNagyobbFajl_ATaroloElutasitja()
    {
        var status = await UploadAsync("attachments/p/toobig.png", "image/png", "image/png", 64, 5000);

        Assert.Equal(400, status);
    }

    [Fact]
    public async Task ABejelentettnelKisebbFajl_ATaroloElutasitja()
    {
        //A pontos hossz mindkét iranyban kot: a meretbejelentes nem lehet puszta formalitas.
        var status = await UploadAsync("attachments/p/toosmall.png", "image/png", "image/png", 5000, 64);

        Assert.Equal(400, status);
    }

    [Fact]
    public async Task ASikeresFeltoltesUtan_AtarolTipusEsMeretVisszaolvashato()
    {
        //A confirm lepes masodik retege erre az adatra epul.
        const string key = "attachments/p/readback.png";
        await UploadAsync(key, "image/png", "image/png", 128, 128);

        var info = await _sut.GetObjectInfoAsync(key);

        Assert.NotNull(info);
        Assert.Equal(128, info!.Size);
        Assert.Equal("image/png", info.ContentType);
    }
}
