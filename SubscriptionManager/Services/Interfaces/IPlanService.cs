using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;

namespace SubscriptionManager.Services.Interfaces
{
    public interface IPlanService
    {
        Task<Page<Plan>> GetPagedAsync(PlanListQuery query, CancellationToken ct = default);
        Task<IEnumerable<Plan>> GetAllAsync(CancellationToken ct = default);
        Task<Plan?> GetByIdAsync(int planId, CancellationToken ct = default);
        Task<int> CreateAsync(PlanFormViewModel vm, int? actorUserId, CancellationToken ct = default);
        Task UpdateAsync(int planId, PlanFormViewModel vm, int? actorUserId, CancellationToken ct = default);
        Task DeleteAsync(int planId, int? actorUserId, CancellationToken ct = default);
    }
}