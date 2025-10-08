using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SubscriptionManager.Data;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Background
{
    public class OutboxDispatcher : BackgroundService
    {
        private readonly IDbConnectionFactory _db;
        private readonly IChannelProducer<NotificationMessage> _notifProducer;
        private readonly ILogService _log;
        private readonly ILogger<OutboxDispatcher> _logger;

        public OutboxDispatcher(IDbConnectionFactory db,
            IChannelProducer<NotificationMessage> notifProducer,
            ILogService log,
            ILogger<OutboxDispatcher> logger)
        {
            _db = db;
            _notifProducer = notifProducer;
            _log = log;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxDispatcher started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var processed = await DispatchBatchAsync(stoppingToken);
                    await Task.Delay(processed == 0 ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(200), stoppingToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox dispatch failed.");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }

        private async Task<int> DispatchBatchAsync(CancellationToken ct)
        {
            using var conn = _db.CreateConnection();
            if (conn.State != ConnectionState.Open)
            {
                if (conn is SqlConnection sql) await sql.OpenAsync(ct);
                else conn.Open();
            }

            const string fetchSql = @"
SELECT TOP 50 OutboxId, [Type], Payload
FROM dbo.OutboxMessages
WHERE ProcessedAt IS NULL AND Attempts < 5
ORDER BY CreatedAt;";

            var items = await conn.QueryAsync<OutboxMessage>(fetchSql);
            int processed = 0;

            foreach (var msg in items)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await HandleAsync(msg, ct);
                    const string okSql = @"UPDATE dbo.OutboxMessages SET ProcessedAt = GETDATE(), [Error] = NULL WHERE OutboxId = @Id;";
                    await conn.ExecuteAsync(okSql, new { Id = msg.OutboxId });
                    processed++;
                }
                catch (Exception ex)
                {
                    const string errSql = @"UPDATE dbo.OutboxMessages SET Attempts = Attempts + 1, [Error] = @Error WHERE OutboxId = @Id;";
                    await conn.ExecuteAsync(errSql, new { Id = msg.OutboxId, Error = ex.Message });
                }
            }

            return processed;
        }

        private async Task HandleAsync(OutboxMessage msg, CancellationToken ct)
        {
            // Example router: turn outbox messages into notifications or other actions
            switch (msg.Type)
            {
                case "PaymentProcessed":
                    var p = JsonSerializer.Deserialize<PaymentNotificationDto>(msg.Payload) ?? new();
                    _notifProducer.TryWrite(new NotificationMessage
                    {
                        UserId = p.UserId,
                        Type = "Email",
                        Subject = "Payment received",
                        Body = $"Payment {p.PaymentId} for subscription {p.SubscriptionId} amount {p.Amount:C} processed."
                    });
                    await _log.WriteLogAsync(new LogMessage { UserId = p.UserId, Action = "Outbox.PaymentProcessed", Message = $"Outbox delivered payment {p.PaymentId}" }, ct);
                    break;

                case "PaymentRefunded":
                    var r = JsonSerializer.Deserialize<PaymentNotificationDto>(msg.Payload) ?? new();
                    _notifProducer.TryWrite(new NotificationMessage
                    {
                        UserId = r.UserId,
                        Type = "Email",
                        Subject = "Payment refunded",
                        Body = $"Payment {r.PaymentId} for subscription {r.SubscriptionId} has been refunded."
                    });
                    break;

                default:
                    // Ignore unknown types filhaal
                    break;
            }
        }

        private class PaymentNotificationDto
        {
            public int UserId { get; set; }
            public int PaymentId { get; set; }
            public int SubscriptionId { get; set; }
            public decimal Amount { get; set; }
        }
    }
}