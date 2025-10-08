using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Background
{
    public class SubscriptionExpiryChecker : BackgroundService
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IChannelProducer<LogMessage> _logProducer;
        private readonly ILogger<SubscriptionExpiryChecker> _logger;

        public SubscriptionExpiryChecker(
            ISubscriptionService subscriptionService,
            IChannelProducer<LogMessage> logProducer,
            ILogger<SubscriptionExpiryChecker> logger)
        {
            _subscriptionService = subscriptionService;
            _logProducer = logProducer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SubscriptionExpiryChecker started.");

            // Run immediately at startup, then daily
            await RunOnce(stoppingToken).ConfigureAwait(false);

            var timer = new PeriodicTimer(TimeSpan.FromDays(1));
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    await RunOnce(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            finally
            {
                timer.Dispose();
                _logger.LogInformation("SubscriptionExpiryChecker stopping.");
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            try
            {
                var count = await _subscriptionService.ExpireDueSubscriptionsAsync(ct).ConfigureAwait(false);

                _logProducer.TryWrite(new LogMessage
                {
                    UserId = null,
                    Action = "ExpiryCheckRan",
                    Message = $"Expired {count} subscription(s) at {DateTime.UtcNow:O}"
                });

                _logger.LogInformation("Expiry check complete. Expired {Count} subscriptions.", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expiry check failed.");
                _logProducer.TryWrite(new LogMessage
                {
                    UserId = null,
                    Action = "ExpiryCheckFailed",
                    Message = ex.Message
                });
            }
        }
    }
}