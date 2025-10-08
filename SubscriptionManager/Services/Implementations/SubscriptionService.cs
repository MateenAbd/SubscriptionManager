using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IDbConnectionFactory _db;
        private readonly IChannelProducer<LogMessage> _logProducer;
        private readonly IChannelProducer<NotificationMessage> _notifProducer;

        public SubscriptionService(
            IDbConnectionFactory db,
            IChannelProducer<LogMessage> logProducer,
            IChannelProducer<NotificationMessage> notifProducer)
        {
            _db = db;
            _logProducer = logProducer;
            _notifProducer = notifProducer;
        }

        public async Task<int> SubscribeAsync(int userId, int planId, string paymentMethod, bool autoRenew, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            using var tx = (conn as SqlConnection)?.BeginTransaction() ?? conn.BeginTransaction();

            try
            {
                // Validate user and plan
                const string userSql = "SELECT COUNT(*) FROM dbo.Users WHERE UserId = @UserId;";
                var userExists = await conn.ExecuteScalarAsync<int>(userSql, new { UserId = userId }, tx);
                if (userExists == 0) throw new InvalidOperationException("User not found.");

                const string planSql = @"SELECT TOP 1 PlanId, [Name] AS PlanName, Price, DurationDays FROM dbo.Plans WHERE PlanId = @PlanId;";
                var plan = await conn.QueryFirstOrDefaultAsync<(int PlanId, string PlanName, decimal Price, int DurationDays)>(planSql, new { PlanId = planId }, tx);
                if (plan.PlanId == 0) throw new InvalidOperationException("Plan not found.");

                var now = DateTime.UtcNow;
                var end = now.AddDays(plan.DurationDays);

                // Create subscription
                const string subSql = @"
INSERT INTO dbo.Subscriptions (UserId, PlanId, StartDate, EndDate, RenewalDate, [Status], AutoRenew)
OUTPUT INSERTED.SubscriptionId
VALUES (@UserId, @PlanId, @StartDate, @EndDate, NULL, 'Active', @AutoRenew);";
                var subscriptionId = await conn.ExecuteScalarAsync<int>(subSql, new
                {
                    UserId = userId,
                    PlanId = planId,
                    StartDate = now,
                    EndDate = end,
                    AutoRenew = autoRenew ? 1 : 0
                }, tx);

                // Payment (mock gateway)
                const string paySql = @"
INSERT INTO dbo.Payments (SubscriptionId, Amount, PaymentDate, PaymentMethod, TransactionId, [Status])
OUTPUT INSERTED.PaymentId
VALUES (@SubscriptionId, @Amount, GETDATE(), @PaymentMethod, @TransactionId, 'Completed');";
                var paymentId = await conn.ExecuteScalarAsync<int>(paySql, new
                {
                    SubscriptionId = subscriptionId,
                    Amount = plan.Price,
                    PaymentMethod = paymentMethod,
                    TransactionId = Guid.NewGuid().ToString()
                }, tx);

                tx.Commit();

                _logProducer.TryWrite(new LogMessage
                {
                    UserId = userId,
                    Action = "SubscriptionCreated",
                    Message = $"User {userId} subscribed to Plan {plan.PlanName} (PlanId={plan.PlanId}), SubId={subscriptionId}."
                });

                _logProducer.TryWrite(new LogMessage
                {
                    UserId = userId,
                    Action = "PaymentProcessed",
                    Message = $"Payment {paymentId} completed for subscription {subscriptionId} amount {plan.Price:C}."
                });

                _notifProducer.TryWrite(new NotificationMessage
                {
                    UserId = userId,
                    Type = "Email",
                    Subject = "Subscription Confirmed",
                    Body = $"Your subscription (ID {subscriptionId}) to {plan.PlanName} is active."
                });

                return subscriptionId;
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        public async Task RenewAsync(int subscriptionId, string paymentMethod, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            using var tx = (conn as SqlConnection)?.BeginTransaction() ?? conn.BeginTransaction();

            try
            {
                const string qSql = @"
SELECT s.SubscriptionId, s.UserId, s.PlanId, s.StartDate, s.EndDate, s.RenewalDate, s.[Status], s.AutoRenew,
       p.Price, p.DurationDays, p.[Name] AS PlanName
FROM dbo.Subscriptions s
JOIN dbo.Plans p ON s.PlanId = p.PlanId
WHERE s.SubscriptionId = @SubscriptionId;";
                var row = await conn.QueryFirstOrDefaultAsync<dynamic>(qSql, new { SubscriptionId = subscriptionId }, tx);
                if (row == null) throw new KeyNotFoundException("Subscription not found.");

                int userId = row.UserId;
                decimal price = row.Price;
                int durationDays = row.DurationDays;
                string planName = row.PlanName;
                DateTime end = row.EndDate;

                var now = DateTime.UtcNow;
                var newEnd = (end > now ? end : now).AddDays(durationDays);

                const string upSql = @"
UPDATE dbo.Subscriptions
SET EndDate = @NewEnd, RenewalDate = GETDATE(), [Status] = 'Active'
WHERE SubscriptionId = @SubscriptionId;";
                await conn.ExecuteAsync(upSql, new { NewEnd = newEnd, SubscriptionId = subscriptionId }, tx);

                // Payment for renewal
                const string paySql = @"
INSERT INTO dbo.Payments (SubscriptionId, Amount, PaymentDate, PaymentMethod, TransactionId, [Status])
VALUES (@SubscriptionId, @Amount, GETDATE(), @PaymentMethod, @TransactionId, 'Completed');";
                await conn.ExecuteAsync(paySql, new
                {
                    SubscriptionId = subscriptionId,
                    Amount = price,
                    PaymentMethod = paymentMethod,
                    TransactionId = Guid.NewGuid().ToString()
                }, tx);

                tx.Commit();

                _logProducer.TryWrite(new LogMessage
                {
                    UserId = userId,
                    Action = "SubscriptionRenewed",
                    Message = $"Subscription {subscriptionId} renewed to {newEnd:yyyy-MM-dd}. Plan {planName}."
                });

                _notifProducer.TryWrite(new NotificationMessage
                {
                    UserId = userId,
                    Type = "Email",
                    Subject = "Subscription Renewed",
                    Body = $"Your subscription (ID {subscriptionId}) has been renewed through {newEnd:d}."
                });
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        public async Task CancelAsync(int subscriptionId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            using var tx = (conn as SqlConnection)?.BeginTransaction() ?? conn.BeginTransaction();

            try
            {
                const string qSql = @"
SELECT s.SubscriptionId, s.UserId, s.PlanId, s.StartDate, s.EndDate, s.[Status], s.AutoRenew,
       p.Price, p.DurationDays, p.[Name] AS PlanName
FROM dbo.Subscriptions s
JOIN dbo.Plans p ON s.PlanId = p.PlanId
WHERE s.SubscriptionId = @SubscriptionId;";
                var srow = await conn.QueryFirstOrDefaultAsync<dynamic>(qSql, new { SubscriptionId = subscriptionId }, tx);
                if (srow == null) throw new KeyNotFoundException("Subscription not found.");

                int userId = srow.UserId;
                int planId = srow.PlanId;
                decimal price = srow.Price;
                int durationDays = srow.DurationDays;
                DateTime prevEnd = srow.EndDate;
                string planName = srow.PlanName;

                var now = DateTime.UtcNow;

                // Compute pro‑rata refund
                var remainingDays = Math.Max(0, (int)Math.Ceiling((prevEnd.Date - now.Date).TotalDays));
                remainingDays = Math.Min(remainingDays, durationDays);
                decimal refundAmount = 0m;
                if (durationDays > 0 && remainingDays > 0)
                {
                    refundAmount = Math.Round(price * remainingDays / durationDays, 2, MidpointRounding.AwayFromZero);
                }

                // Cancel subscription
                const string cancelSql = @"
UPDATE dbo.Subscriptions
SET [Status] = 'Cancelled', EndDate = @Now, AutoRenew = 0
WHERE SubscriptionId = @SubscriptionId;";
                await conn.ExecuteAsync(cancelSql, new { Now = now, SubscriptionId = subscriptionId }, tx);

                // Refund latest completed payment, if any
                const string lastPaySql = @"
SELECT TOP 1 PaymentId, Amount
FROM dbo.Payments
WHERE SubscriptionId = @SubscriptionId AND [Status] = 'Completed'
ORDER BY PaymentDate DESC;";
                var payment = await conn.QueryFirstOrDefaultAsync<(int PaymentId, decimal Amount)>(lastPaySql, new { SubscriptionId = subscriptionId }, tx);

                if (payment.PaymentId != 0 && refundAmount > 0)
                {
                    const string refundSql = @"UPDATE dbo.Payments SET [Status] = 'Refunded' WHERE PaymentId = @PaymentId;";
                    await conn.ExecuteAsync(refundSql, new { payment.PaymentId }, tx);
                }

                tx.Commit();

                _logProducer.TryWrite(new LogMessage
                {
                    UserId = userId,
                    Action = "SubscriptionCancelled",
                    Message = $"Subscription {subscriptionId} cancelled. Pro‑rata refund: {refundAmount:C} for plan {planName}."
                });

                _notifProducer.TryWrite(new NotificationMessage
                {
                    UserId = userId,
                    Type = "Email",
                    Subject = "Subscription Cancelled",
                    Body = $"Your subscription (ID {subscriptionId}) was cancelled. Refund amount: {refundAmount:C}."
                });
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        public async Task<Page<Subscription>> GetPagedAsync(SubscriptionListQuery query, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            string sortColumn = (query.SortBy ?? "StartDate").ToLowerInvariant() switch
            {
                "startdate" => "s.StartDate",
                "enddate" => "s.EndDate",
                "status" => "s.[Status]",
                _ => "s.StartDate"
            };
            string sortDir = (query.SortDir ?? "desc").ToLowerInvariant() == "asc" ? "ASC" : "DESC";

            var where = "WHERE 1=1";
            var dp = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(query.UserEmail))
            {
                where += " AND u.Email LIKE @UserEmail";
                dp.Add("@UserEmail", $"%{query.UserEmail}%");
            }
            if (query.PlanId.HasValue)
            {
                where += " AND s.PlanId = @PlanId";
                dp.Add("@PlanId", query.PlanId.Value);
            }
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                where += " AND s.[Status] = @Status";
                dp.Add("@Status", query.Status);
            }
            if (query.From.HasValue)
            {
                where += " AND s.StartDate >= @From";
                dp.Add("@From", query.From.Value);
            }
            if (query.To.HasValue)
            {
                where += " AND s.StartDate < DATEADD(DAY, 1, @To)";
                dp.Add("@To", query.To.Value);
            }

            dp.Add("@Offset", (query.Page - 1) * query.PageSize);
            dp.Add("@PageSize", query.PageSize);

            var countSql = $@"SELECT COUNT(*)
FROM dbo.Subscriptions s
JOIN dbo.Users u ON s.UserId = u.UserId
JOIN dbo.Plans p ON s.PlanId = p.PlanId
{where};";

            var itemsSql = $@"
SELECT s.SubscriptionId, s.UserId, s.PlanId, s.StartDate, s.EndDate, s.RenewalDate, s.[Status], s.AutoRenew,
       u.Email AS UserEmail, p.[Name] AS PlanName
FROM dbo.Subscriptions s
JOIN dbo.Users u ON s.UserId = u.UserId
JOIN dbo.Plans p ON s.PlanId = p.PlanId
{where}
ORDER BY {sortColumn} {sortDir}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var total = await conn.ExecuteScalarAsync<int>(countSql, dp);
            var items = await conn.QueryAsync<Subscription>(itemsSql, dp);

            return Page<Subscription>.Create(items.ToList(), total, query.Page, query.PageSize);
        }

        public async Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(int userId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string sql = @"
SELECT s.SubscriptionId, s.UserId, s.PlanId, s.StartDate, s.EndDate, s.RenewalDate, s.[Status], s.AutoRenew,
       p.[Name] AS PlanName
FROM dbo.Subscriptions s
JOIN dbo.Plans p ON s.PlanId = p.PlanId
WHERE s.UserId = @UserId
ORDER BY s.StartDate DESC;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var list = await conn.QueryAsync<Subscription>(sql, new { UserId = userId });
            return list;
        }

        public async Task<int> ExpireDueSubscriptionsAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            // Collect due subscription ids first
            const string selSql = @"SELECT SubscriptionId, UserId FROM dbo.Subscriptions WHERE [Status] = 'Active' AND EndDate <= GETDATE();";
            var due = (await conn.QueryAsync<(int SubscriptionId, int UserId)>(selSql)).ToList();
            if (!due.Any()) return 0;

            const string updSql = @"UPDATE dbo.Subscriptions SET [Status] = 'Expired' WHERE [Status] = 'Active' AND EndDate <= GETDATE();";
            var affected = await conn.ExecuteAsync(updSql);

            foreach (var s in due)
            {
                _logProducer.TryWrite(new LogMessage
                {
                    UserId = s.UserId,
                    Action = "SubscriptionExpired",
                    Message = $"Subscription {s.SubscriptionId} expired."
                });
                _notifProducer.TryWrite(new NotificationMessage
                {
                    UserId = s.UserId,
                    Type = "Email",
                    Subject = "Subscription Expired",
                    Body = $"Your subscription (ID {s.SubscriptionId}) has expired."
                });
            }

            return affected;
        }

        public async Task<Subscription?> GetByIdAsync(int subscriptionId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string sql = @"
SELECT s.SubscriptionId, s.UserId, s.PlanId, s.StartDate, s.EndDate, s.RenewalDate, s.[Status], s.AutoRenew,
       u.Email AS UserEmail, p.[Name] AS PlanName
FROM dbo.Subscriptions s
JOIN dbo.Users u ON s.UserId = u.UserId
JOIN dbo.Plans p ON s.PlanId = p.PlanId
WHERE s.SubscriptionId = @SubscriptionId;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            return await conn.QueryFirstOrDefaultAsync<Subscription>(sql, new { SubscriptionId = subscriptionId });
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