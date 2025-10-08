using System.Threading;
using System.Threading.Tasks;
using SubscriptionManager.Models.Domain;

namespace SubscriptionManager.Services.Interfaces
{
    public interface ILogService
    {
        Task WriteLogAsync(LogMessage message, CancellationToken ct = default);
    }
}