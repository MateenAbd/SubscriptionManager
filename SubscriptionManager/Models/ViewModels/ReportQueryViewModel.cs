using System;
using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.ViewModels
{
    public class ReportQueryViewModel
    {
        [Required]
        public DateTime From { get; set; }

        [Required]
        public DateTime To { get; set; }
    }

    public class PlanMetricsItem
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int TotalSubscriptions { get; set; }
        public int CancelledSubscriptions { get; set; }
        public decimal ChurnRate => TotalSubscriptions == 0 ? 0 : (decimal)CancelledSubscriptions / TotalSubscriptions;
    }

    public class RevenueSummary
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}