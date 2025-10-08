using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SubscriptionManager.Data;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly IDbConnectionFactory _db;
        private readonly IChannelProducer<LogMessage> _logProducer;

        public PaymentService(IDbConnectionFactory db, IChannelProducer<LogMessage> logProducer)
        {
            _db = db;
            _logProducer = logProducer;
        }

        public async Task<Page<Payment>> GetPagedAsync(PaymentFilterViewModel filter, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var sortColumn = (filter.SortBy ?? "PaymentDate").ToLowerInvariant() switch
            {
                "paymentdate" => "PaymentDate",
                "amount" => "Amount",
                "status" => "[Status]",
                _ => "PaymentDate"
            };
            var sortDir = (filter.SortDir ?? "desc").ToLowerInvariant() == "asc" ? "ASC" : "DESC";

            var where = "WHERE 1=1";
            var dp = new DynamicParameters();
            if (filter.From.HasValue)
            {
                where += " AND PaymentDate >= @From";
                dp.Add("@From", filter.From.Value);
            }
            if (filter.To.HasValue)
            {
                where += " AND PaymentDate < DATEADD(DAY, 1, @To)";
                dp.Add("@To", filter.To.Value);
            }
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                where += " AND [Status] = @Status";
                dp.Add("@Status", filter.Status);
            }

            dp.Add("@Offset", (filter.Page - 1) * filter.PageSize);
            dp.Add("@PageSize", filter.PageSize);

            var countSql = $"SELECT COUNT(*) FROM dbo.Payments {where};";
            var itemsSql = $@"
SELECT PaymentId, SubscriptionId, Amount, PaymentDate, PaymentMethod, TransactionId, [Status]
FROM dbo.Payments
{where}
ORDER BY {sortColumn} {sortDir}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var total = await conn.ExecuteScalarAsync<int>(countSql, dp);
            var items = await conn.QueryAsync<Payment>(itemsSql, dp);

            return Page<Payment>.Create(items, total, filter.Page, filter.PageSize);
        }

        public async Task<int> RecordPaymentAsync(int subscriptionId, decimal amount, string method, string status = "Completed", CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string sql = @"
INSERT INTO dbo.Payments (SubscriptionId, Amount, PaymentDate, PaymentMethod, TransactionId, [Status])
OUTPUT INSERTED.PaymentId
VALUES (@SubscriptionId, @Amount, GETDATE(), @Method, @TxnId, @Status);";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var paymentId = await conn.ExecuteScalarAsync<int>(sql, new
            {
                SubscriptionId = subscriptionId,
                Amount = amount,
                Method = method,
                TxnId = Guid.NewGuid().ToString(),
                Status = status
            });

            _logProducer.TryWrite(new LogMessage
            {
                Action = "PaymentProcessed",
                Message = $"Payment {paymentId} recorded for subscription {subscriptionId} amount {amount:C}."
            });

            return paymentId;
        }

        public async Task RefundAsync(int paymentId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            using var tx = (conn as SqlConnection)?.BeginTransaction() ?? conn.BeginTransaction();

            try
            {
                // Get payment + subscription context
                const string qSql = @"
SELECT p.PaymentId, p.SubscriptionId, p.Amount, p.[Status] AS PaymentStatus,
       s.UserId, s.EndDate, s.[Status] AS SubStatus, s.PlanId,
       pl.DurationDays, pl.Price, pl.[Name] AS PlanName
FROM dbo.Payments p
JOIN dbo.Subscriptions s ON p.SubscriptionId = s.SubscriptionId
JOIN dbo.Plans pl ON s.PlanId = pl.PlanId
WHERE p.PaymentId = @PaymentId;";
                var row = await conn.QueryFirstOrDefaultAsync<dynamic>(qSql, new { PaymentId = paymentId }, tx);
                if (row == null) throw new KeyNotFoundException("Payment not found.");

                int subscriptionId = row.SubscriptionId;
                int userId = row.UserId;
                string planName = row.PlanName;
                int durationDays = row.DurationDays;
                decimal amount = row.Amount;
                DateTime endDate = row.EndDate;

                // Set payment to Refunded
                const string upPay = @"UPDATE dbo.Payments SET [Status] = 'Refunded' WHERE PaymentId = @PaymentId;";
                await conn.ExecuteAsync(upPay, new { PaymentId = paymentId }, tx);

                // Optionally cancel subscription if still active
                const string upSub = @"
UPDATE dbo.Subscriptions
SET [Status] = CASE WHEN [Status] = 'Active' THEN 'Cancelled' ELSE [Status] END,
    EndDate = CASE WHEN [Status] = 'Active' THEN GETDATE() ELSE EndDate END,
    AutoRenew = 0
WHERE SubscriptionId = @SubscriptionId;";
                await conn.ExecuteAsync(upSub, new { SubscriptionId = subscriptionId }, tx);

                tx.Commit();

                // Compute simple pro‑rata info for log (best effort)
                var now = DateTime.UtcNow;
                var remainingDays = Math.Max(0, (int)Math.Ceiling((endDate.Date - now.Date).TotalDays));
                remainingDays = Math.Min(remainingDays, durationDays);
                decimal refundEstimate = durationDays > 0 ? Math.Round(amount * remainingDays / durationDays, 2, MidpointRounding.AwayFromZero) : 0m;

                _logProducer.TryWrite(new LogMessage
                {
                    UserId = userId,
                    Action = "PaymentRefunded",
                    Message = $"Payment {paymentId} refunded for subscription {subscriptionId}. Estimated refund: {refundEstimate:C}. Plan {planName}."
                });
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        public async Task<decimal> GetTotalAsync(DateTime? from, DateTime? to, string? status, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var where = "WHERE 1=1";
            var dp = new DynamicParameters();

            if (from.HasValue)
            {
                where += " AND PaymentDate >= @From";
                dp.Add("@From", from.Value);
            }
            if (to.HasValue)
            {
                where += " AND PaymentDate < DATEADD(DAY, 1, @To)";
                dp.Add("@To", to.Value);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                where += " AND [Status] = @Status";
                dp.Add("@Status", status);
            }

            var sql = $"SELECT COALESCE(SUM(Amount), 0) FROM dbo.Payments {where};";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            return await conn.ExecuteScalarAsync<decimal>(sql, dp);
        }

        public async Task<IEnumerable<Payment>> GetBySubscriptionAsync(int subscriptionId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string sql = @"
SELECT PaymentId, SubscriptionId, Amount, PaymentDate, PaymentMethod, TransactionId, [Status]
FROM dbo.Payments
WHERE SubscriptionId = @SubscriptionId
ORDER BY PaymentDate DESC;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var items = await conn.QueryAsync<Payment>(sql, new { SubscriptionId = subscriptionId });
            return items;
        }

        private static async Task EnsureOpenAsync(IDbConnection conn, CancellationToken ct)
        {
            if (conn.State != ConnectionState.Open)
            {
                if (conn is SqlConnection sql)
                    await sql.OpenAsync(ct);
                else
                    conn.Open();
            }
        }
    }
}