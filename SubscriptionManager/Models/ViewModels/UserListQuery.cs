using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.ViewModels
{
    public class UserListQuery
    {
        [StringLength(100)]
        public string? Search { get; set; }

        [StringLength(50)]
        public string? SortBy { get; set; } = "RegistrationDate"; // Email | RegistrationDate | FirstName
        [StringLength(4)]
        public string? SortDir { get; set; } = "desc";

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 200)]
        public int PageSize { get; set; } = 10;
    }
}