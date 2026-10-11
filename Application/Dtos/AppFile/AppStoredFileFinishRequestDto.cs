namespace Application.Dtos.AppFile
{
    /// <summary>The driver finishing an upload it started with create-app-stored-file.</summary>
    public class AppStoredFileFinishRequestDto
    {
        public int AppStoredFileId { get; set; }
        /// <summary>Why the upload was cancelled (cancel only).</summary>
        public string? Message { get; set; }
    }
}
