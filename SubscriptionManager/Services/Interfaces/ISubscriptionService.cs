using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;

namespace SubscriptionManager.Services.Interfaces
{
    public interface ISubscriptionService
    {
        Task<int> SubscribeAsync(int userId, int planId, string paymentMethod, bool autoRenew, CancellationToken ct = default);
        Task RenewAsync(int subscriptionId, string paymentMethod, CancellationToken ct = default);
        Task CancelAsync(int subscriptionId, CancellationToken ct = default);
        Task<Page<Subscription>> GetPagedAsync(SubscriptionListQuery query, CancellationToken ct = default);
        Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(int userId, CancellationToken ct = default);
        Task<int> ExpireDueSubscriptionsAsync(CancellationToken ct = default); // returns count expired
        Task<Subscription?> GetByIdAsync(int subscriptionId, CancellationToken ct = default);
    }
}