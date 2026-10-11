namespace Domain.Entitites.ApplicationContextDb
{
    public class UserApiKey : BaseUserOwnedEntity
    {
        public string ApiKey { get; set; }
        public DateTime? LastUsed { get; set; }
        public bool IsActive { get; set; }
    }
}

