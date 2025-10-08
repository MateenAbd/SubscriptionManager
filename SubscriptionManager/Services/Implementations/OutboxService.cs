using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SubscriptionManager.Data;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Services.Implementations
{
    public class OutboxService : IOutboxService
    {
        private readonly IDbConnectionFactory _db;
        public OutboxService(IDbConnectionFactory db) => _db = db;

        public async Task<long> EnqueueAsync(string type, string jsonPayload, CancellationToken ct = default)
        {
            using var conn = _db.CreateConnection();
            if (conn.State != ConnectionState.Open)
            {
                if (conn is SqlConnection sqlConn) await sqlConn.OpenAsync(ct);
                else conn.Open();
            }

            const string sql = @"
INSERT INTO dbo.OutboxMessages ([Type], Payload)
OUTPUT INSERTED.OutboxId
VALUES (@Type, @Payload);";

            return await conn.ExecuteScalarAsync<long>(sql, new { Type = type, Payload = jsonPayload });
        }
    }
}