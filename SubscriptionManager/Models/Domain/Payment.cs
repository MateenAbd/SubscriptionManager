using System;
using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.Domain
{
    public class Payment
    {
        public int PaymentId { get; set; }

        [Required]
        public int SubscriptionId { get; set; }

        [Range(0, 999999)]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        [Required, StringLength(100)]
        public string TransactionId { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Status { get; set; } = PaymentStatuses.Completed;
    }
}