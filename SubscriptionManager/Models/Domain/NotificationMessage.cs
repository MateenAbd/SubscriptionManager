using System;

namespace SubscriptionManager.Models.Domain
{
    public class NotificationMessage
    {
        public int? UserId { get; set; }
        public string Type { get; set; } = "Info"; // e.g., Email, SMS, Info
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}