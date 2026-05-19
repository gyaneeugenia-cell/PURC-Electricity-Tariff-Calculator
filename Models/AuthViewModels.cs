using System.ComponentModel.DataAnnotations;

namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class RegisterViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Confirm Password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public string? ResetToken { get; set; }
}

public sealed class ResetPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Reset Token")]
    public string ResetToken { get; set; } = string.Empty;

    [Required]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }
}

public sealed class AdminDashboardViewModel
{
    public IReadOnlyList<AppUserSummary> Users { get; init; } = Array.Empty<AppUserSummary>();

    public IReadOnlyList<AuthAuditLogEntry> AuditLogs { get; init; } = Array.Empty<AuthAuditLogEntry>();

    public TariffAdminUpdateInput TariffUpdate { get; set; } = new();

    public IReadOnlyList<TariffCategoryOption> TariffCategoryChoices { get; init; } = Array.Empty<TariffCategoryOption>();

    public IReadOnlyList<string> ComponentTypeSuggestions { get; init; } = Array.Empty<string>();

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }
}
