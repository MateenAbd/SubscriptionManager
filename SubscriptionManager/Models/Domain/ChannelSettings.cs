namespace SubscriptionManager.Models.Domain
{
    public class ChannelSettings
    {
        public int LoggingCapacity { get; set; } = 1000;
        public int NotificationCapacity { get; set; } = 500;
    }
}