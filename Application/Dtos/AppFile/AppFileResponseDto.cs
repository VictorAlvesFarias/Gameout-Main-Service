namespace Application.Dtos.AppFile
{
    public class AppFileResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool VersionControl { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public string UserId { get; set; }
        public int Status { get; set; }
        public string StatusDetails { get; set; }
        public string StatusMessage { get; set; }
        /// <summary>Date of the latest confirmed version: the driver compares its folder with it.</summary>
        public DateTime? LastSyncedAt { get; set; }
        /// <summary>Size of the folder (before zipping) in the latest confirmed version.</summary>
        public double? LastSyncedSize { get; set; }
    }
}
