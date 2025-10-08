using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Services.Implementations
{
    public class ChannelProducer<T> : IChannelProducer<T>
    {
        private readonly ChannelWriter<T> _writer;

        public ChannelProducer(ChannelWriter<T> writer)
        {
            _writer = writer;
        }

        public bool TryWrite(T message) => _writer.TryWrite(message);

        public async Task WriteAsync(T message, CancellationToken ct = default)
        {
            while (await _writer.WaitToWriteAsync(ct).ConfigureAwait(false))
            {
                if (_writer.TryWrite(message)) return;
            }
        }
    }
}