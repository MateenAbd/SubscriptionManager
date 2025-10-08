using System.Collections.Generic;
using SubscriptionManager.Models.Domain;

namespace SubscriptionManager.Models.ViewModels
{
    public class DashboardViewModel
    {
        public User? CurrentUser { get; set; }
        public IEnumerable<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public IEnumerable<Payment> Payments { get; set; } = new List<Payment>();
    }
}