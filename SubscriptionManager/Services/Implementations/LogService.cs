using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SubscriptionManager.Data;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Services.Implementations
{
    public class LogService : ILogService
    {
        private readonly IDbConnectionFactory _db;

        public LogService(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task WriteLogAsync(LogMessage message, CancellationToken ct = default)
        {
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var p = new DynamicParameters();
            p.Add("@UserId", message.UserId);
            p.Add("@Action", message.Action);
            p.Add("@Message", message.Message);

            await conn.ExecuteAsync("dbo.sp_InsertLog", p, commandType: CommandType.StoredProcedure);
        }

        private static async Task EnsureOpenAsync(IDbConnection conn, CancellationToken ct)
        {
            if (conn.State != ConnectionState.Open)
            {
                if (conn is SqlConnection sql) await sql.OpenAsync(ct);
                else conn.Open();
            }
        }
    }
}