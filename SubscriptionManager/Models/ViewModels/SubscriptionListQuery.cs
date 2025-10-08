using System;
using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.ViewModels
{
    public class SubscriptionListQuery
    {
        [StringLength(100)]
        public string? UserEmail { get; set; }

        public int? PlanId { get; set; }

        [StringLength(20)]
        public string? Status { get; set; } // Active | Cancelled | Expired

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        [StringLength(50)]
        public string? SortBy { get; set; } = "StartDate"; // StartDate | EndDate | Status
        [StringLength(4)]
        public string? SortDir { get; set; } = "desc";

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 200)]
        public int PageSize { get; set; } = 10;
    }
}