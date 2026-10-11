using Application.Configuration;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Services.StorageService
{
    public class MinioStorageService : IMinioStorageService
    {
        private readonly MinioOptions _options;

        public MinioStorageService(IOptions<MinioOptions> options)
        {
            _options = options.Value;
        }

        public string GetObjectKey(int storedFileId)
        {
            return $"stored-files/{storedFileId}.zip";
        }

        public Task<BaseResponse<string>> GenerateUploadUrlAsync(int storedFileId)
        {
            return GenerateUrlAsync(storedFileId, isDownload: false);
        }

        public Task<BaseResponse<string>> GenerateDownloadUrlAsync(int storedFileId)
        {
            return GenerateUrlAsync(storedFileId, isDownload: true);
        }

        private async Task<BaseResponse<string>> GenerateUrlAsync(int storedFileId, bool isDownload)
        {
            var response = new BaseResponse<string>();

            if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
            {
                response.AddError(new ErrorMessage("MinIO configuration is missing"));
                return response;
            }

            var client = new MinioClient()
                .WithEndpoint(_options.Endpoint)
                .WithCredentials(_options.AccessKey, _options.SecretKey)
                .WithSSL(_options.UseSsl)
                .Build();

            var bucketName = _options.BucketName;
            if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName)))
            {
                await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
            }

            // The signature covers the host, so the URL is signed for the address its users reach.
            // The region is fixed so signing doesn't need to ask the (unreachable) public host.
            var signer = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
                ? client
                : new MinioClient()
                    .WithEndpoint(_options.PublicEndpoint)
                    .WithCredentials(_options.AccessKey, _options.SecretKey)
                    .WithSSL(_options.UseSsl)
                    .WithRegion(_options.Region)
                    .Build();

            var objectKey = GetObjectKey(storedFileId);
            var expiration = (int)TimeSpan.FromMinutes(Math.Max(1, _options.ExpirationMinutes)).TotalSeconds;
            var url = isDownload
                ? await signer.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithExpiry(expiration))
                : await signer.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithExpiry(expiration));

            response.Success = true;
            response.Data = url;
            return response;
        }
    }
}