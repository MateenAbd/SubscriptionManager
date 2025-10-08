using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;

namespace SubscriptionManager.Services.Interfaces
{
    public interface IUserService
    {
        Task<Page<User>> GetPagedAsync(UserListQuery query, CancellationToken ct = default);
        Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);
        Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
        Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
        Task<int> RegisterAsync(RegisterViewModel vm, CancellationToken ct = default);
        Task<int> CreateAsync(UserFormViewModel vm, CancellationToken ct = default);
        Task UpdateAsync(int userId, UserFormViewModel vm, CancellationToken ct = default);
        Task DeleteAsync(int userId, int actorUserId, CancellationToken ct = default);
        Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(int userId, CancellationToken ct = default);
        Task<IEnumerable<Payment>> GetUserPaymentsAsync(int userId, CancellationToken ct = default);
        Task EnsureSeededPasswordHashesAsync(CancellationToken ct = default);
    }
}