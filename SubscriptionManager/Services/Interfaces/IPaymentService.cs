using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;

namespace SubscriptionManager.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<Page<Payment>> GetPagedAsync(PaymentFilterViewModel filter, CancellationToken ct = default);
        Task<int> RecordPaymentAsync(int subscriptionId, decimal amount, string method, string status = "Completed", CancellationToken ct = default);
        Task RefundAsync(int paymentId, CancellationToken ct = default);
        Task<decimal> GetTotalAsync(System.DateTime? from, System.DateTime? to, string? status, CancellationToken ct = default);
        Task<IEnumerable<Payment>> GetBySubscriptionAsync(int subscriptionId, CancellationToken ct = default);
    }
}