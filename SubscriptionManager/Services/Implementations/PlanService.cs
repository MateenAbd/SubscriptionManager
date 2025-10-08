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
    public class PlanService : IPlanService
    {
        private readonly IDbConnectionFactory _db;
        private readonly IChannelProducer<LogMessage> _logProducer;

        public PlanService(IDbConnectionFactory db, IChannelProducer<LogMessage> logProducer)
        {
            _db = db;
            _logProducer = logProducer;
        }

        public async Task<Page<Plan>> GetPagedAsync(PlanListQuery query, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var sortColumn = (query.SortBy ?? "CreatedAt").ToLowerInvariant() switch
            {
                "name" => "[Name]",
                "price" => "Price",
                _ => "CreatedAt"
            };
            var sortDir = (query.SortDir ?? "desc").ToLowerInvariant() == "asc" ? "ASC" : "DESC";

            var where = "WHERE 1=1";
            var dp = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                where += " AND ([Name] LIKE @Search OR BillingCycle LIKE @Search)";
                dp.Add("@Search", $"%{query.Search}%");
            }
            if (!string.IsNullOrWhiteSpace(query.BillingCycle))
            {
                where += " AND BillingCycle = @BillingCycle";
                dp.Add("@BillingCycle", query.BillingCycle);
            }

            dp.Add("@Offset", (query.Page - 1) * query.PageSize);
            dp.Add("@PageSize", query.PageSize);

            var countSql = $"SELECT COUNT(*) FROM dbo.Plans {where};";
            var itemsSql = $@"
SELECT PlanId, [Name], [Description], Price, BillingCycle, DurationDays, Features, CreatedAt, UpdatedAt
FROM dbo.Plans
{where}
ORDER BY {sortColumn} {sortDir}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var total = await conn.ExecuteScalarAsync<int>(countSql, dp);
            var items = await conn.QueryAsync<Plan>(itemsSql, dp);

            return Page<Plan>.Create(items.ToList(), total, query.Page, query.PageSize);
        }

        public async Task<IEnumerable<Plan>> GetAllAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            const string sql = @"SELECT PlanId, [Name], [Description], Price, BillingCycle, DurationDays, Features, CreatedAt, UpdatedAt
                                 FROM dbo.Plans ORDER BY CreatedAt DESC;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var items = await conn.QueryAsync<Plan>(sql);
            return items;
        }

        public async Task<Plan?> GetByIdAsync(int planId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            const string sql = @"SELECT PlanId, [Name], [Description], Price, BillingCycle, DurationDays, Features, CreatedAt, UpdatedAt
                                 FROM dbo.Plans WHERE PlanId = @PlanId;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            return await conn.QueryFirstOrDefaultAsync<Plan>(sql, new { PlanId = planId });
        }

        public async Task<int> CreateAsync(PlanFormViewModel vm, int? actorUserId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            
            var p = new DynamicParameters();
            p.Add("@Name", vm.Name);
            p.Add("@Description", vm.Description);
            p.Add("@Price", vm.Price);
            p.Add("@BillingCycle", vm.BillingCycle);
            p.Add("@DurationDays", vm.DurationDays);
            p.Add("@Features", vm.Features);
            p.Add("@PlanId", dbType: DbType.Int32, direction: ParameterDirection.Output);

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            await conn.ExecuteAsync("dbo.sp_InsertPlan", p, commandType: CommandType.StoredProcedure);
            int planId = p.Get<int>("@PlanId");

            _logProducer.TryWrite(new LogMessage
            {
                UserId = actorUserId,
                Action = "PlanCreated",
                Message = $"Plan '{vm.Name}' (Id={planId}) created."
            });

            return planId;
        }

        public async Task UpdateAsync(int planId, PlanFormViewModel vm, int? actorUserId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string existsSql = "SELECT COUNT(*) FROM dbo.Plans WHERE [Name] = @Name AND PlanId <> @PlanId;";
            const string updateSql = @"
UPDATE dbo.Plans
SET [Name] = @Name,
    [Description] = @Description,
    Price = @Price,
    BillingCycle = @BillingCycle,
    DurationDays = @DurationDays,
    Features = @Features
WHERE PlanId = @PlanId;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var dup = await conn.ExecuteScalarAsync<int>(existsSql, new { vm.Name, PlanId = planId });
            if (dup > 0) throw new InvalidOperationException("A plan with the same name already exists.");

            var rows = await conn.ExecuteAsync(updateSql, new
            {
                PlanId = planId,
                vm.Name,
                vm.Description,
                vm.Price,
                vm.BillingCycle,
                vm.DurationDays,
                vm.Features
            });

            if (rows == 0) throw new KeyNotFoundException("Plan not found.");

            _logProducer.TryWrite(new LogMessage
            {
                UserId = actorUserId,
                Action = "PlanUpdated",
                Message = $"Plan '{vm.Name}' (Id={planId}) updated."
            });
        }

        public async Task DeleteAsync(int planId, int? actorUserId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string refSql = "SELECT COUNT(*) FROM dbo.Subscriptions WHERE PlanId = @PlanId;";
            const string deleteSql = "DELETE FROM dbo.Plans WHERE PlanId = @PlanId;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var refsCount = await conn.ExecuteScalarAsync<int>(refSql, new { PlanId = planId });
            if (refsCount > 0) throw new InvalidOperationException("Cannot delete plan with existing subscriptions.");

            var rows = await conn.ExecuteAsync(deleteSql, new { PlanId = planId });
            if (rows == 0) throw new KeyNotFoundException("Plan not found.");

            _logProducer.TryWrite(new LogMessage
            {
                UserId = actorUserId,
                Action = "PlanDeleted",
                Message = $"Plan (Id={planId}) deleted."
            });
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