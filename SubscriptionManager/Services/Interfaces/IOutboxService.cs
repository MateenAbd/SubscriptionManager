using System.Threading;
using System.Threading.Tasks;

namespace SubscriptionManager.Services.Interfaces
{
    public interface IOutboxService
    {
        Task<long> EnqueueAsync(string type, string jsonPayload, CancellationToken ct = default);
    }
}