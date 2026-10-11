using Application.Types;

namespace Application.Dtos.AppFile
{
    public class UpdateAppFileStatusRequestDto
    {
        public int AppFileId { get; set; }
        public AppFileStatusTypes Status { get; set; }
        /// <summary>What the driver found (locked file, missing folder...), shown on the web.</summary>
        public string? Message { get; set; }
    }
}
