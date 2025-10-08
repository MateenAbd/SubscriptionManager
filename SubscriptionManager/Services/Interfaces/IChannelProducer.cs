using System.Threading;
using System.Threading.Tasks;

namespace SubscriptionManager.Services.Interfaces
{
    public interface IChannelProducer<T>
    {
        bool TryWrite(T message);
        Task WriteAsync(T message, CancellationToken ct = default);
    }
}