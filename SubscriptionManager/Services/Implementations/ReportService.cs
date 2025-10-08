using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SubscriptionManager.Data;
using SubscriptionManager.Models.ViewModels;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly IDbConnectionFactory _db;
        public ReportService(IDbConnectionFactory db) => _db = db;

        public async Task<decimal> GetRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            
            const string sql = "EXEC dbo.sp_CalculateRevenue @Start, @End;";
            var revenue = await conn.ExecuteScalarAsync<decimal>(sql, new { Start = from, End = to });
            return revenue;
        }

        public async Task<double> GetAverageSubscriptionDurationDaysAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string sql = @"
SELECT AVG(CAST(DATEDIFF(DAY, StartDate, EndDate) AS FLOAT))
FROM dbo.Subscriptions;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var avg = await conn.ExecuteScalarAsync<double?>(sql);
            return avg ?? 0.0;
        }

        public async Task<IEnumerable<PlanMetricsItem>> GetPlanMetricsAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var where = "WHERE 1=1";
            var dp = new DynamicParameters();
            if (from.HasValue)
            {
                where += " AND s.StartDate >= @From";
                dp.Add("@From", from.Value);
            }
            if (to.HasValue)
            {
                where += " AND s.StartDate < DATEADD(DAY, 1, @To)";
                dp.Add("@To", to.Value);
            }

            var sql = $@"
SELECT
    p.PlanId,
    p.[Name] AS PlanName,
    COUNT(*) AS TotalSubscriptions,
    SUM(CASE WHEN s.[Status] = 'Cancelled' THEN 1 ELSE 0 END) AS CancelledSubscriptions
FROM dbo.Subscriptions s
JOIN dbo.Plans p ON s.PlanId = p.PlanId
{where}
GROUP BY p.PlanId, p.[Name]
ORDER BY PlanName;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var list = await conn.QueryAsync<PlanMetricsItem>(sql, dp);
            return list;
        }

        public async Task<decimal> GetChurnRateAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string sql = @"
SELECT
    CAST(SUM(CASE WHEN [Status] = 'Cancelled' THEN 1 ELSE 0 END) AS DECIMAL(18,4)) /
    NULLIF(CAST(COUNT(*) AS DECIMAL(18,4)), 0)
FROM dbo.Subscriptions;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var rate = await conn.ExecuteScalarAsync<decimal?>(sql);
            return rate ?? 0m;
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