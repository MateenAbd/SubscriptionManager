using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly ILogService _logService;

        public NotificationService(ILogger<NotificationService> logger, ILogService logService)
        {
            _logger = logger;
            _logService = logService;
        }

        public async Task HandleAsync(NotificationMessage message, CancellationToken ct = default)
        {
            // Simulate sending (email/SMS). actually we should integrate with provider
            _logger.LogInformation("Notify UserId={UserId} Type={Type} Subject={Subject}", message.UserId, message.Type, message.Subject);
            Console.WriteLine($"[Notify] {message.Type} to User {message.UserId}: {message.Subject}");

            // Log that a notification was sent
            await _logService.WriteLogAsync(new LogMessage
            {
                UserId = message.UserId,
                Action = "NotificationSent",
                Message = $"{message.Type}: {message.Subject}"
            }, ct);
        }
    }
}