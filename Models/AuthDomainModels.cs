namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class StoredAppUser
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public string PasswordSalt { get; init; } = string.Empty;

    public string RoleName { get; init; } = "User";

    public bool IsDeleted { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public DateTimeOffset? PasswordChangedAt { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }

    public string? DeletedByEmail { get; init; }
}

public sealed class AppUserSummary
{
    public string UserId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleName { get; init; } = "User";

    public bool IsDeleted { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public DateTimeOffset? PasswordChangedAt { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }

    public string? DeletedByEmail { get; init; }
}

public sealed class AuthAuditLogEntry
{
    public long LogId { get; init; }

    public string? ActorEmail { get; init; }

    public string? TargetEmail { get; init; }

    public string ActionName { get; init; } = string.Empty;

    public string? Details { get; init; }

    public DateTimeOffset HappenedAt { get; init; }
}
