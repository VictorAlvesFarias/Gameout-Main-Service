namespace Domain.Queues.AppFileDtos
{
    public static class DriverEventTypes
    {
        public const string Heartbeat = "Heartbeat";
        public const string Progress = "Progress";
        public const string Error = "Error";
        public const string Completed = "Completed";
        public const string Started = "Started";
    }

    public class DriverEventMessage
    {
        public string Event { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int AppFileId { get; set; }
        public int? AppStoredFileId { get; set; }
        public string TraceId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public int? Progress { get; set; }
        public string? Status { get; set; }
        public string? ObjectKey { get; set; }
        public long? OriginalFileSize { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}