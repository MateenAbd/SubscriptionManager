using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Background
{
    public class NotificationChannelConsumer : BackgroundService
    {
        private readonly ChannelReader<NotificationMessage> _reader;
        private readonly INotificationService _notifService;
        private readonly ILogger<NotificationChannelConsumer> _logger;

        public NotificationChannelConsumer(ChannelReader<NotificationMessage> reader, INotificationService notifService, ILogger<NotificationChannelConsumer> logger)
        {
            _reader = reader;
            _notifService = notifService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationChannelConsumer started.");
            try
            {
                while (await _reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                {
                    while (_reader.TryRead(out var message))
                    {
                        try
                        {
                            await _notifService.HandleAsync(message, stoppingToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process notification {Subject}", message.Subject);
                            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            finally
            {
                _logger.LogInformation("NotificationChannelConsumer stopping.");
            }
        }
    }
}