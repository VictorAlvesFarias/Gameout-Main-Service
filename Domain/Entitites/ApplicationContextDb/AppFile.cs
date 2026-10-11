namespace Domain.Entitites.ApplicationContextDb
{
    /// <summary>
    /// A group of saves. Which folder feeds it, and when, is decided by each driver on its own PC.
    /// </summary>
    public class AppFile : BaseUserOwnedEntity
    {
        public string Name { get; set; }
        public bool VersionControl { get; set; }
        public int Status { get; set; }
        public string StatusDetails { get; set; }
        public string StatusMessage { get; set; }

        public void Update(string? name = null, bool? versionControl = null, int? status = null, string? statusDetails = null, string? statusMessage = null)
        {
            Name = name ?? Name;
            VersionControl = versionControl ?? VersionControl;
            UpdateDate = DateTime.UtcNow;
            Status = status ?? Status;
            StatusDetails = statusDetails ?? StatusDetails;
            StatusMessage = statusMessage ?? StatusMessage;
        }
    }
}
