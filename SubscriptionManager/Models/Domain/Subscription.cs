using System;
using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.Domain
{
    public class Subscription
    {
        public int SubscriptionId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int PlanId { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime EndDate { get; set; }

        public DateTime? RenewalDate { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = SubscriptionStatuses.Active;

        public bool AutoRenew { get; set; } = true;

        // Optional helper fields for JOINs in views
        public string? UserEmail { get; set; }
        public string? PlanName { get; set; }
    }
}