namespace Application.Dtos.AppFile
{
    public class AppStoredFileCreateResponseDto
    {
        public int AppStoredFileId { get; set; }
        public int DiskFileId { get; set; }
        public int Status { get; set; }
        public string UploadUrl { get; set; } = string.Empty;
    }
}