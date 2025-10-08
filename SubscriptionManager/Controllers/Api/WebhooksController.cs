using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using SubscriptionManager.Data;
using SubscriptionManager.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace SubscriptionManager.Controllers.Api
{
    [ApiController]
    [Route("api/webhooks")]
    [AllowAnonymous]
    [EnableRateLimiting("webhooks")]
    public class WebhooksController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IDbConnectionFactory _db;
        private readonly IOutboxService _outbox;

        public WebhooksController(IConfiguration config, IDbConnectionFactory db, IOutboxService outbox)
        {
            _config = config;
            _db = db;
            _outbox = outbox;
        }

        [HttpPost("payments")]
        public async Task<IActionResult> Payments(CancellationToken ct)
        {
            var secret = _config["Payments:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(secret))
                return StatusCode(500, "Webhook secret not configured.");

            // Read headers
            var sig = Request.Headers["X-Signature"].ToString();
            var eventId = Request.Headers["X-Event-Id"].ToString();
            if (string.IsNullOrWhiteSpace(eventId))
                return BadRequest("Missing X-Event-Id header.");

            // Read body
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            // Verify HMAC SHA256 signature as base64(hmac(secret, body))
            if (!VerifyHmac(body, secret, sig))
                return Unauthorized();

            using var conn = (SqlConnection)_db.CreateConnection();
            await conn.OpenAsync(ct);

            // Idempotency: insert event row (unique EventId)
            const string insertEvent = @"
BEGIN TRY
    INSERT INTO dbo.WebhookEvents (EventId, Signature, RawPayload, Processed) VALUES (@EventId, @Signature, @RawPayload, 0);
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 2627 RETURN; -- duplicate EventId, already processed/received
    ELSE THROW;
END CATCH";
            await conn.ExecuteAsync(insertEvent, new { EventId = eventId, Signature = sig, RawPayload = body });

            // Parse minimal payload: { "type": "payment.succeeded", "transactionId":"...", "status":"Completed", "amount":123.45, "userId":1, "subscriptionId": 10, "paymentId": 25 }
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var type = root.GetProperty("type").GetString() ?? string.Empty;
            var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "Completed" : "Completed";
            var txnId = root.TryGetProperty("transactionId", out var tx) ? tx.GetString() : null;
            var amount = root.TryGetProperty("amount", out var amt) && amt.TryGetDecimal(out var a) ? a : 0m;
            var userId = root.TryGetProperty("userId", out var uid) && uid.TryGetInt32(out var u) ? u : (int?)null;
            var subscriptionId = root.TryGetProperty("subscriptionId", out var sid) && sid.TryGetInt32(out var s) ? s : (int?)null;
            var paymentId = root.TryGetProperty("paymentId", out var pid) && pid.TryGetInt32(out var p) ? p : (int?)null;

            // Update payment by TransactionId or PaymentId
            if (!string.IsNullOrWhiteSpace(txnId))
            {
                const string upByTxn = @"UPDATE dbo.Payments SET [Status] = @Status WHERE TransactionId = @TransactionId;";
                await conn.ExecuteAsync(upByTxn, new { Status = status, TransactionId = txnId });
            }
            else if (paymentId.HasValue)
            {
                const string upById = @"UPDATE dbo.Payments SET [Status] = @Status WHERE PaymentId = @PaymentId;";
                await conn.ExecuteAsync(upById, new { Status = status, PaymentId = paymentId.Value });
            }

            // Enqueue outbox for notification
            var payload = new
            {
                Type = type,
                UserId = userId ?? 0,
                SubscriptionId = subscriptionId ?? 0,
                PaymentId = paymentId ?? 0,
                Amount = amount
            };
            var json = JsonSerializer.Serialize(payload);
            var outboxType = type == "payment.refunded" ? "PaymentRefunded" : "PaymentProcessed";
            await _outbox.EnqueueAsync(outboxType, json, ct);

            
            const string markProcessed = @"UPDATE dbo.WebhookEvents SET Processed = 1 WHERE EventId = @EventId;";
            await conn.ExecuteAsync(markProcessed, new { EventId = eventId });

            return Ok(new { received = true });
        }

        private static bool VerifyHmac(string body, string secret, string headerSignature)
        {
            try
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                var computed = Convert.ToBase64String(hash);
                
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computed),
                    Encoding.UTF8.GetBytes(headerSignature ?? string.Empty));
            }
            catch { return false; }
        }
    }
}