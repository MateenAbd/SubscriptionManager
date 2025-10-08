namespace SubscriptionManager.Models.Domain
{
    public class JwtSettings
    {
        public string Issuer { get; set; } = "SubscriptionManager";
        public string Audience { get; set; } = "SubscriptionManager";
        public string Key { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; } = 60;
    }
}