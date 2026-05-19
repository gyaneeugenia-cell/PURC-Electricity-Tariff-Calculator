using Microsoft.Extensions.Configuration;
using Npgsql;
using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public sealed class PostgresAuthRepository : IAuthRepository
{
    private readonly string _connectionString;

    public PostgresAuthRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SupabasePostgres")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "The SupabasePostgres connection string is missing. Set it in appsettings or an environment variable.");
        }
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS purc_app_users (
                user_id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                password_salt TEXT NOT NULL,
                role_name TEXT NOT NULL DEFAULT 'User',
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                last_login_at TIMESTAMPTZ NULL,
                password_changed_at TIMESTAMPTZ NULL,
                deleted_at TIMESTAMPTZ NULL,
                deleted_by_email TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS purc_auth_audit_logs (
                log_id BIGSERIAL PRIMARY KEY,
                actor_email TEXT NULL,
                target_email TEXT NULL,
                action_name TEXT NOT NULL,
                details TEXT NULL,
                happened_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS purc_password_reset_tokens (
                token_id TEXT PRIMARY KEY,
                email TEXT NOT NULL,
                token_hash TEXT NOT NULL,
                expires_at TIMESTAMPTZ NOT NULL,
                consumed_at TIMESTAMPTZ NULL
            );
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM purc_app_users
            WHERE is_deleted = FALSE;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<StoredAppUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT user_id, display_name, email, password_hash, password_salt, role_name, is_deleted,
                   created_at, last_login_at, password_changed_at, deleted_at, deleted_by_email
            FROM purc_app_users
            WHERE LOWER(email) = LOWER(@email)
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StoredAppUser
        {
            UserId = reader.GetString(0),
            DisplayName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            PasswordSalt = reader.GetString(4),
            RoleName = reader.GetString(5),
            IsDeleted = reader.GetBoolean(6),
            CreatedAt = ReadTimestamp(reader, 7) ?? DateTimeOffset.UtcNow,
            LastLoginAt = ReadTimestamp(reader, 8),
            PasswordChangedAt = ReadTimestamp(reader, 9),
            DeletedAt = ReadTimestamp(reader, 10),
            DeletedByEmail = reader.IsDBNull(11) ? null : reader.GetString(11),
        };
    }

    public async Task CreateUserAsync(
        string displayName,
        string email,
        string passwordHash,
        string passwordSalt,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO purc_app_users (
                user_id, display_name, email, password_hash, password_salt, role_name, created_at
            )
            VALUES (
                @userId, @displayName, @email, @passwordHash, @passwordSalt, @roleName, @createdAt
            );
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("displayName", displayName.Trim());
        command.Parameters.AddWithValue("email", email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("passwordHash", passwordHash);
        command.Parameters.AddWithValue("passwordSalt", passwordSalt);
        command.Parameters.AddWithValue("roleName", roleName.Trim());
        command.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ReactivateUserAsync(
        string displayName,
        string email,
        string passwordHash,
        string passwordSalt,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE purc_app_users
            SET display_name = @displayName,
                password_hash = @passwordHash,
                password_salt = @passwordSalt,
                role_name = @roleName,
                is_deleted = FALSE,
                deleted_at = NULL,
                deleted_by_email = NULL,
                password_changed_at = @changedAt
            WHERE LOWER(email) = LOWER(@email);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("displayName", displayName.Trim());
        command.Parameters.AddWithValue("email", email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("passwordHash", passwordHash);
        command.Parameters.AddWithValue("passwordSalt", passwordSalt);
        command.Parameters.AddWithValue("roleName", roleName.Trim());
        command.Parameters.AddWithValue("changedAt", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE purc_app_users
            SET last_login_at = @loggedAt
            WHERE LOWER(email) = LOWER(@email);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("loggedAt", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("email", email.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdatePasswordAsync(
        string email,
        string passwordHash,
        string passwordSalt,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE purc_app_users
            SET password_hash = @passwordHash,
                password_salt = @passwordSalt,
                password_changed_at = @changedAt
            WHERE LOWER(email) = LOWER(@email)
              AND is_deleted = FALSE;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("passwordHash", passwordHash);
        command.Parameters.AddWithValue("passwordSalt", passwordSalt);
        command.Parameters.AddWithValue("changedAt", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("email", email.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CreateResetTokenAsync(
        string email,
        string tokenId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        const string deleteSql = """
            DELETE FROM purc_password_reset_tokens
            WHERE LOWER(email) = LOWER(@email)
              OR expires_at <= NOW();
            """;

        const string insertSql = """
            INSERT INTO purc_password_reset_tokens (token_id, email, token_hash, expires_at)
            VALUES (@tokenId, @email, @tokenHash, @expiresAt);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection))
        {
            deleteCommand.Parameters.AddWithValue("email", email.Trim());
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var insertCommand = new NpgsqlCommand(insertSql, connection);
        insertCommand.Parameters.AddWithValue("tokenId", tokenId);
        insertCommand.Parameters.AddWithValue("email", email.Trim());
        insertCommand.Parameters.AddWithValue("tokenHash", tokenHash);
        insertCommand.Parameters.AddWithValue("expiresAt", expiresAt);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ValidateResetTokenAsync(
        string email,
        Func<string, bool> tokenVerifier,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT token_hash
            FROM purc_password_reset_tokens
            WHERE LOWER(email) = LOWER(@email)
              AND consumed_at IS NULL
              AND expires_at > NOW()
            ORDER BY expires_at DESC
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email.Trim());
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is string tokenHash && tokenVerifier(tokenHash);
    }

    public async Task ConsumeResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE purc_password_reset_tokens
            SET consumed_at = NOW()
            WHERE LOWER(email) = LOWER(@email)
              AND consumed_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppUserSummary>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT user_id, display_name, email, role_name, is_deleted, created_at, last_login_at, password_changed_at, deleted_at, deleted_by_email
            FROM purc_app_users
            ORDER BY created_at DESC;
            """;

        var users = new List<AppUserSummary>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new AppUserSummary
            {
                UserId = reader.GetString(0),
                DisplayName = reader.GetString(1),
                Email = reader.GetString(2),
                RoleName = reader.GetString(3),
                IsDeleted = reader.GetBoolean(4),
                CreatedAt = ReadTimestamp(reader, 5) ?? DateTimeOffset.UtcNow,
                LastLoginAt = ReadTimestamp(reader, 6),
                PasswordChangedAt = ReadTimestamp(reader, 7),
                DeletedAt = ReadTimestamp(reader, 8),
                DeletedByEmail = reader.IsDBNull(9) ? null : reader.GetString(9),
            });
        }

        return users;
    }

    public async Task<IReadOnlyList<AuthAuditLogEntry>> GetAuditLogsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT log_id, actor_email, target_email, action_name, details, happened_at
            FROM purc_auth_audit_logs
            ORDER BY happened_at DESC
            LIMIT 200;
            """;

        var logs = new List<AuthAuditLogEntry>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(new AuthAuditLogEntry
            {
                LogId = reader.GetInt64(0),
                ActorEmail = reader.IsDBNull(1) ? null : reader.GetString(1),
                TargetEmail = reader.IsDBNull(2) ? null : reader.GetString(2),
                ActionName = reader.GetString(3),
                Details = reader.IsDBNull(4) ? null : reader.GetString(4),
                HappenedAt = ReadTimestamp(reader, 5) ?? DateTimeOffset.UtcNow,
            });
        }

        return logs;
    }

    public async Task UpdateRoleAsync(
        string targetEmail,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE purc_app_users
            SET role_name = @roleName
            WHERE LOWER(email) = LOWER(@email)
              AND is_deleted = FALSE;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("roleName", roleName.Trim());
        command.Parameters.AddWithValue("email", targetEmail.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SoftDeleteUserAsync(
        string targetEmail,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE purc_app_users
            SET is_deleted = TRUE,
                deleted_at = NOW(),
                deleted_by_email = @actorEmail
            WHERE LOWER(email) = LOWER(@targetEmail)
              AND is_deleted = FALSE;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("targetEmail", targetEmail.Trim());
        command.Parameters.AddWithValue("actorEmail", actorEmail.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task HardDeleteUserAsync(string targetEmail, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM purc_app_users
            WHERE LOWER(email) = LOWER(@email);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", targetEmail.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task LogAuditAsync(
        string? actorEmail,
        string? targetEmail,
        string actionName,
        string? details,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO purc_auth_audit_logs (actor_email, target_email, action_name, details, happened_at)
            VALUES (@actorEmail, @targetEmail, @actionName, @details, @happenedAt);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("actorEmail", (object?)actorEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("targetEmail", (object?)targetEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("actionName", actionName);
        command.Parameters.AddWithValue("details", (object?)details ?? DBNull.Value);
        command.Parameters.AddWithValue("happenedAt", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset? ReadTimestamp(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => DateTimeOffset.Parse(value.ToString()!)
        };
    }
}