using System.Threading.Channels;
using Microsoft.Extensions.Options;
using SubscriptionManager.Models.Domain;

namespace SubscriptionManager.Channels
{
    public class ChannelProvider
    {
        public Channel<LogMessage> LogChannel { get; }
        public Channel<NotificationMessage> NotificationChannel { get; }

        public ChannelReader<LogMessage> LogReader => LogChannel.Reader;
        public ChannelWriter<LogMessage> LogWriter => LogChannel.Writer;

        public ChannelReader<NotificationMessage> NotificationReader => NotificationChannel.Reader;
        public ChannelWriter<NotificationMessage> NotificationWriter => NotificationChannel.Writer;

        public ChannelProvider(IOptions<ChannelSettings> settings)
        {
            var s = settings.Value ?? new ChannelSettings();

            var logOpts = new BoundedChannelOptions(s.LoggingCapacity > 0 ? s.LoggingCapacity : 1000)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            };
            LogChannel = Channel.CreateBounded<LogMessage>(logOpts);

            var notifOpts = new BoundedChannelOptions(s.NotificationCapacity > 0 ? s.NotificationCapacity : 500)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            };
            NotificationChannel = Channel.CreateBounded<NotificationMessage>(notifOpts);
        }
    }
}