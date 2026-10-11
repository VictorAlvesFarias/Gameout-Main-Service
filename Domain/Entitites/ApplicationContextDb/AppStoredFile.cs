namespace Domain.Entitites.ApplicationContextDb
{
    public class AppStoredFile : BaseUserOwnedEntity
    {
        public int AppFileId { get; set; }
        public AppFile AppFile { get; set; }
        public int? StoredFileId { get; set; }
        public StoredFile? StoredFile { get; set; }
        /// <summary>
        /// The record is created before the driver uploads the zip; it only becomes a version once the
        /// driver confirms the upload.
        /// </summary>
        public bool Completed { get; set; }

        public void Update(int? appFileId = null, int? storedFileId = null)
        {
            AppFileId = appFileId ?? AppFileId;
            StoredFileId = storedFileId ?? StoredFileId;
        }

        public void Complete()
        {
            Completed = true;
            UpdateDate = DateTime.UtcNow;
        }
    }
}
