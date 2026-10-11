namespace Application.Dtos.Driver
{
    public class DriverStatusResponseDto
    {
        /// <summary>Last request made with one of the user's API keys (UTC); null when no driver ever connected.</summary>
        public DateTime? LastSeen { get; set; }
    }
}
