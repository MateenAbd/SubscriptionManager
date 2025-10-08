using System;

namespace SubscriptionManager.Models.Domain
{
    public class LogMessage
    {
        public int? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}