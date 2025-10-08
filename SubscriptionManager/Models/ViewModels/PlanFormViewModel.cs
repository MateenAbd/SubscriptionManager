using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.ViewModels
{
    public class PlanFormViewModel
    {
        public int? PlanId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(int.MaxValue)]
        public string? Description { get; set; }

        [Range(0, 999999)]
        public decimal Price { get; set; }

        [Required, StringLength(20)]
        public string BillingCycle { get; set; } = "Monthly";

        [Range(1, 2000)]
        public int DurationDays { get; set; }

        public string? Features { get; set; }
    }
}