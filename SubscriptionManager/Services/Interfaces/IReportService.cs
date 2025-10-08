using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SubscriptionManager.Models.ViewModels;

namespace SubscriptionManager.Services.Interfaces
{
    public interface IReportService
    {
        Task<decimal> GetRevenueAsync(System.DateTime from, System.DateTime to, CancellationToken ct = default);
        Task<double> GetAverageSubscriptionDurationDaysAsync(CancellationToken ct = default);
        Task<IEnumerable<PlanMetricsItem>> GetPlanMetricsAsync(System.DateTime? from, System.DateTime? to, CancellationToken ct = default);
        Task<decimal> GetChurnRateAsync(CancellationToken ct = default); // cancelled/total
    }
}