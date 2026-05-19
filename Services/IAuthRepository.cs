using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public interface IAuthRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task<int> CountActiveUsersAsync(CancellationToken cancellationToken = default);

    Task<StoredAppUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task CreateUserAsync(
        string displayName,
        string email,
        string passwordHash,
        string passwordSalt,
        string roleName,
        CancellationToken cancellationToken = default);

    Task UpdateLastLoginAsync(string email, CancellationToken cancellationToken = default);

    Task UpdatePasswordAsync(
        string email,
        string passwordHash,
        string passwordSalt,
        CancellationToken cancellationToken = default);

    Task CreateResetTokenAsync(
        string email,
        string tokenId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateResetTokenAsync(
        string email,
        Func<string, bool> tokenVerifier,
        CancellationToken cancellationToken = default);

    Task ConsumeResetTokenAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppUserSummary>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthAuditLogEntry>> GetAuditLogsAsync(CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(
        string targetEmail,
        string roleName,
        CancellationToken cancellationToken = default);

    Task SoftDeleteUserAsync(
        string targetEmail,
        string actorEmail,
        CancellationToken cancellationToken = default);
    Task ReactivateUserAsync(
        string displayName,
        string email,
        string passwordHash,
        string passwordSalt,
        string roleName,
        CancellationToken cancellationToken = default);   
    Task HardDeleteUserAsync(
        string targetEmail,
        CancellationToken cancellationToken = default);
    Task LogAuditAsync(
        string? actorEmail,
        string? targetEmail,
        string actionName,
        string? details,
        CancellationToken cancellationToken = default);
}
