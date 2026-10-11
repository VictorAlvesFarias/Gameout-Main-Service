namespace Application.Dtos.AppFile
{
    public class AppFileRequestDto
    {
        public string Name { get; set; }
        /// <summary>Keeps every version; off, a confirmed upload replaces the previous one. Null keeps the current value.</summary>
        public bool? VersionControl { get; set; }
    }
}
