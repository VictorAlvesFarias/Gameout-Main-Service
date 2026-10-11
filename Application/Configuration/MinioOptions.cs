namespace Application.Configuration
{
    public sealed class MinioOptions
    {
        public const string SectionName = "MinIO";

        public string Endpoint { get; set; } = string.Empty;
        /// <summary>
        /// Address the presigned URLs are signed for, when clients reach MinIO through another host
        /// than the API (in Docker: API -> minio:9000, driver and browser -> localhost:9000).
        /// Empty uses <see cref="Endpoint"/>.
        /// </summary>
        public string PublicEndpoint { get; set; } = string.Empty;
        public string Region { get; set; } = "us-east-1";
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = "gameout";
        public bool UseSsl { get; set; }
        public int ExpirationMinutes { get; set; } = 30;
    }
}