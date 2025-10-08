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
    public class LogChannelConsumer : BackgroundService
    {
        private readonly ChannelReader<LogMessage> _reader;
        private readonly ILogService _logService;
        private readonly ILogger<LogChannelConsumer> _logger;

        public LogChannelConsumer(ChannelReader<LogMessage> reader, ILogService logService, ILogger<LogChannelConsumer> logger)
        {
            _reader = reader;
            _logService = logService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LogChannelConsumer started.");
            try
            {
                while (await _reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                {
                    while (_reader.TryRead(out var message))
                    {
                        try
                        {
                            await _logService.WriteLogAsync(message, stoppingToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to write log for action {Action}", message.Action);
                            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            finally
            {
                _logger.LogInformation("LogChannelConsumer stopping.");
            }
        }
    }
}