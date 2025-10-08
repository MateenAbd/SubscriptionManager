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
    public class UserService : IUserService
    {
        private readonly IDbConnectionFactory _db;
        private readonly IChannelProducer<LogMessage> _logProducer;

        public UserService(IDbConnectionFactory db, IChannelProducer<LogMessage> logProducer)
        {
            _db = db;
            _logProducer = logProducer;
        }

        public async Task<Page<User>> GetPagedAsync(UserListQuery query, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var sortColumn = (query.SortBy ?? "RegistrationDate").ToLowerInvariant() switch
            {
                "email" => "Email",
                "firstname" => "FirstName",
                _ => "RegistrationDate"
            };
            var sortDir = (query.SortDir ?? "desc").ToLowerInvariant() == "asc" ? "ASC" : "DESC";

            var where = "WHERE 1=1";
            var dp = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                where += " AND (Email LIKE @Search OR FirstName LIKE @Search OR LastName LIKE @Search)";
                dp.Add("@Search", $"%{query.Search}%");
            }

            dp.Add("@Offset", (query.Page - 1) * query.PageSize);
            dp.Add("@PageSize", query.PageSize);

            var countSql = $"SELECT COUNT(*) FROM dbo.Users {where};";
            var itemsSql = $@"
SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.Phone, u.[Address], u.RegistrationDate, u.[Role], u.PasswordHash
FROM dbo.Users u
{where}
ORDER BY {sortColumn} {sortDir}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var total = await conn.ExecuteScalarAsync<int>(countSql, dp);
            var items = await conn.QueryAsync<User>(itemsSql, dp);

            return Page<User>.Create(items.ToList(), total, query.Page, query.PageSize);
        }

        public async Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            const string sql = @"SELECT UserId, FirstName, LastName, Email, Phone, [Address], RegistrationDate, [Role], PasswordHash
                                 FROM dbo.Users WHERE UserId = @UserId;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            return await conn.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            const string sql = @"SELECT TOP 1 UserId, FirstName, LastName, Email, Phone, [Address], RegistrationDate, [Role], PasswordHash
                                 FROM dbo.Users WHERE Email = @Email;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            const string sql = @"SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email) THEN 1 ELSE 0 END;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var exists = await conn.ExecuteScalarAsync<int>(sql, new { Email = email });
            return exists == 1;
        }

        public async Task<int> RegisterAsync(RegisterViewModel vm, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (await EmailExistsAsync(vm.Email, ct))
                throw new InvalidOperationException("Email already in use.");

            var hash = BCrypt.Net.BCrypt.HashPassword(vm.Password);

            const string sql = @"
INSERT INTO dbo.Users (FirstName, LastName, Email, Phone, [Address], [Role], PasswordHash, RegistrationDate)
OUTPUT INSERTED.UserId
VALUES (@FirstName, @LastName, @Email, @Phone, @Address, @Role, @PasswordHash, GETDATE());";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var userId = await conn.ExecuteScalarAsync<int>(sql, new
            {
                vm.FirstName,
                vm.LastName,
                vm.Email,
                vm.Phone,
                vm.Address,
                Role = AppRoles.Subscriber,
                PasswordHash = hash
            });

            _logProducer.TryWrite(new LogMessage
            {
                UserId = userId,
                Action = "UserRegistered",
                Message = $"User {vm.Email} registered."
            });

            return userId;
        }

        public async Task<int> CreateAsync(UserFormViewModel vm, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (await EmailExistsAsync(vm.Email, ct))
                throw new InvalidOperationException("Email already in use.");

            var password = string.IsNullOrWhiteSpace(vm.NewPassword) ? "P@ssw0rd!" : vm.NewPassword!;
            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            const string sql = @"
INSERT INTO dbo.Users (FirstName, LastName, Email, Phone, [Address], [Role], PasswordHash, RegistrationDate)
OUTPUT INSERTED.UserId
VALUES (@FirstName, @LastName, @Email, @Phone, @Address, @Role, @PasswordHash, GETDATE());";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var userId = await conn.ExecuteScalarAsync<int>(sql, new
            {
                vm.FirstName,
                vm.LastName,
                vm.Email,
                vm.Phone,
                vm.Address,
                vm.Role,
                PasswordHash = hash
            });

            _logProducer.TryWrite(new LogMessage
            {
                UserId = userId,
                Action = "UserCreated",
                Message = $"User {vm.Email} created with role {vm.Role}."
            });

            return userId;
        }

        public async Task UpdateAsync(int userId, UserFormViewModel vm, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string dupSql = "SELECT COUNT(*) FROM dbo.Users WHERE Email = @Email AND UserId <> @UserId;";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var dup = await conn.ExecuteScalarAsync<int>(dupSql, new { vm.Email, UserId = userId });
            if (dup > 0) throw new InvalidOperationException("Email already in use by another user.");

            string? hash = null;
            if (!string.IsNullOrWhiteSpace(vm.NewPassword))
                hash = BCrypt.Net.BCrypt.HashPassword(vm.NewPassword);

            const string updateSql = @"
UPDATE dbo.Users
SET FirstName = @FirstName,
    LastName = @LastName,
    Email = @Email,
    Phone = @Phone,
    [Address] = @Address,
    [Role] = @Role,
    PasswordHash = COALESCE(@PasswordHash, PasswordHash)
WHERE UserId = @UserId;";

            var rows = await conn.ExecuteAsync(updateSql, new
            {
                UserId = userId,
                vm.FirstName,
                vm.LastName,
                vm.Email,
                vm.Phone,
                vm.Address,
                vm.Role,
                PasswordHash = hash
            });

            if (rows == 0) throw new KeyNotFoundException("User not found.");

            _logProducer.TryWrite(new LogMessage
            {
                UserId = userId,
                Action = "UserUpdated",
                Message = $"User {vm.Email} updated."
            });
        }

        public async Task DeleteAsync(int userId, int actorUserId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string refSql = "SELECT COUNT(*) FROM dbo.Subscriptions WHERE UserId = @UserId;";
            const string delSql = "DELETE FROM dbo.Users WHERE UserId = @UserId;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var refsCount = await conn.ExecuteScalarAsync<int>(refSql, new { UserId = userId });
            if (refsCount > 0) throw new InvalidOperationException("Cannot delete user with existing subscriptions.");

            var rows = await conn.ExecuteAsync(delSql, new { UserId = userId });
            if (rows == 0) throw new KeyNotFoundException("User not found.");

            _logProducer.TryWrite(new LogMessage
            {
                UserId = actorUserId,
                Action = "UserDeleted",
                Message = $"User (Id={userId}) deleted by Admin (Id={actorUserId})."
            });
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
            var items = await conn.QueryAsync<Subscription>(sql, new { UserId = userId });
            return items;
        }

        public async Task<IEnumerable<Payment>> GetUserPaymentsAsync(int userId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string sql = @"
SELECT pm.PaymentId, pm.SubscriptionId, pm.Amount, pm.PaymentDate, pm.PaymentMethod, pm.TransactionId, pm.[Status]
FROM dbo.Payments pm
JOIN dbo.Subscriptions s ON pm.SubscriptionId = s.SubscriptionId
WHERE s.UserId = @UserId
ORDER BY pm.PaymentDate DESC;";

            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);
            var items = await conn.QueryAsync<Payment>(sql, new { UserId = userId });
            return items;
        }

        public async Task EnsureSeededPasswordHashesAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            const string selectSql = @"SELECT UserId, PasswordHash FROM dbo.Users WHERE PasswordHash LIKE 'BCRYPT:%';";
            using var conn = _db.CreateConnection();
            await EnsureOpenAsync(conn, ct);

            var toFix = (await conn.QueryAsync<(int UserId, string PasswordHash)>(selectSql)).ToList();
            if (!toFix.Any()) return;

            using var tx = (conn as SqlConnection)?.BeginTransaction() ?? conn.BeginTransaction();

            try
            {
                const string updateSql = @"UPDATE dbo.Users SET PasswordHash = @PasswordHash WHERE UserId = @UserId;";

                foreach (var row in toFix)
                {
                    ct.ThrowIfCancellationRequested();
                    // PasswordHash stored as "BCRYPT:<plaintext>"
                    var plain = row.PasswordHash.StartsWith("BCRYPT:", StringComparison.OrdinalIgnoreCase)
                        ? row.PasswordHash.Substring("BCRYPT:".Length)
                        : row.PasswordHash;

                    var hash = BCrypt.Net.BCrypt.HashPassword(plain);
                    await conn.ExecuteAsync(updateSql, new { PasswordHash = hash, UserId = row.UserId }, tx);
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
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