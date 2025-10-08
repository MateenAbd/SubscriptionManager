using System;
using System.ComponentModel.DataAnnotations;

namespace SubscriptionManager.Models.Domain
{
    public class User
    {
        public int UserId { get; set; }

        [Required, StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Phone, StringLength(15)]
        public string? Phone { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        [Required, StringLength(20)]
        public string Role { get; set; } = AppRoles.Subscriber;

        [Required, StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;
    }
}