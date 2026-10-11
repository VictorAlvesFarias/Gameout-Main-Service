namespace Domain.Queues.AppFileDtos
{
    public static class DriverCommandTypes
    {
        public const string StartBackup = "StartBackup";
        public const string RefreshWatchers = "RefreshWatchers";
        public const string ScanDirectories = "ScanDirectories";
        public const string DeleteBackup = "DeleteBackup";
        public const string UpdateConfiguration = "UpdateConfiguration";
        public const string Heartbeat = "Heartbeat";
    }

    public class DriverCommandMessage
    {
        public string Command { get; set; } = string.Empty;
        public int AppFileId { get; set; }
        public int? AppStoredFileId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string? PayloadJson { get; set; }
    }
}