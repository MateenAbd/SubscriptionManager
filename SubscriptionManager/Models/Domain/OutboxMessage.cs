namespace SubscriptionManager.Models.Domain
{
    public class OutboxMessage
    {
        public long OutboxId { get; set; }
        public string Type { get; set; } = string.Empty; // e.g., PaymentProcessed, Notification.Email
        public string Payload { get; set; } = string.Empty; // JSON mai rakhunga
    }
}