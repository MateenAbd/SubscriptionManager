using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.ViewModels
{
    public class SubscriptionCreateViewModel
    {
        [Required]
        public int PlanId { get; set; }

        [Required, StringLength(50)]
        public string PaymentMethod { get; set; } = "Credit Card";

        public bool AutoRenew { get; set; } = true;
    }
}