using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace AutoOcrs.Infrastructure.Services;

public class MinioStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageService> _logger;
    private readonly string _bucketName = "adms-documents";

    public MinioStorageService(IConfiguration configuration, ILogger<MinioStorageService> logger)
    {
        _logger = logger;
        
        string endpoint = configuration["Minio:Endpoint"] ?? "localhost:9000";
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            endpoint = uri.Authority;
        }
        
        string accessKey = configuration["Minio:AccessKey"] ?? "minioadmin";
        string secretKey = configuration["Minio:SecretKey"] ?? "minioadmin";
        bool secure = configuration.GetValue<bool>("Minio:Secure");

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(secure)
            .Build();
    }

    public async Task EnsureBucketAsync()
    {
        try
        {
            var beArgs = new BucketExistsArgs().WithBucket(_bucketName);
            bool found = await _minioClient.BucketExistsAsync(beArgs);
            if (!found)
            {
                var mbArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await _minioClient.MakeBucketAsync(mbArgs);
                _logger.LogInformation($"Đã tạo bucket MinIO: {_bucketName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Lỗi khi kiểm tra/tạo bucket MinIO: {_bucketName}");
        }
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType = "application/octet-stream", bool keepExactName = false)
    {
        var objectName = keepExactName ? fileName : $"{Guid.NewGuid():N}_{fileName}";

        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(args);

        return objectName;
    }

    public async Task<string> MoveFileAsync(string sourceObjectName, string targetObjectName)
    {
        if (sourceObjectName == targetObjectName) return targetObjectName;

        var copyArgs = new CopyObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(targetObjectName)
            .WithCopyObjectSource(new CopySourceObjectArgs().WithBucket(_bucketName).WithObject(sourceObjectName));
        
        await _minioClient.CopyObjectAsync(copyArgs);

        var removeArgs = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(sourceObjectName);

        await _minioClient.RemoveObjectAsync(removeArgs);

        return targetObjectName;
    }

    public async Task<Stream> DownloadFileAsync(string objectName)
    {
        var memoryStream = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            });

        await _minioClient.GetObjectAsync(args);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DeleteFileAsync(string objectName)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName);

        await _minioClient.RemoveObjectAsync(args);
    }

    public async Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 3600)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        return await _minioClient.PresignedGetObjectAsync(args);
    }
    
    public async Task<string> GetPresignedPutUrlAsync(string objectName, int expirySeconds = 3600)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        return await _minioClient.PresignedPutObjectAsync(args);
    }
}
