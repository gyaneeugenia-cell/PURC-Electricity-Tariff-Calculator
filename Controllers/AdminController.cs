using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurcHistoricTariffReckoner.CSharp.Models;
using PurcHistoricTariffReckoner.CSharp.Services;

namespace PurcHistoricTariffReckoner.CSharp.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public sealed class AdminController : Controller
{
    private static readonly TariffCategoryOption[] TariffCategoryChoices =
    [
        new() { Code = "Residential", Label = "Residential" },
        new() { Code = "Non-Residential", Label = "Non-Residential" },
        new() { Code = "SLT-LV", Label = "SLT-LV" },
        new() { Code = "SLT-MV", Label = "SLT-MV" },
        new() { Code = "SLT-MV-HV", Label = "SLT-MV-HV" },
        new() { Code = "SLT-MV2", Label = "SLT-MV2" },
        new() { Code = "SLT-HV", Label = "SLT-HV" },
        new() { Code = "SLT-HV-MINES", Label = "SLT-HV-MINES" },
        new() { Code = "SLT-HV-STEEL-COMPANIES", Label = "SLT-HV-STEEL-COMPANIES" },
        new() { Code = "EV-CHARGING", Label = "EV-CHARGING" },
    ];

    private static readonly string[] ComponentTypeSuggestions =
    [
        "energy",
        "service_charge",
        "service_charge_lifeline",
        "fixed_charge",
        "energy_lv",
        "service_charge_lv",
        "demand_charge_lv",
        "energy_mv",
        "service_charge_mv",
        "demand_charge_mv",
        "energy_mv_hv",
        "service_charge_mv_hv",
        "demand_charge_mv_hv",
        "energy_mv2",
        "service_charge_mv2",
        "demand_charge_mv2",
        "energy_hv",
        "service_charge_hv",
        "demand_charge_hv",
        "energy_hv_mines",
        "service_charge_hv_mines",
        "demand_charge_hv_mines",
        "energy_hv_steel_companies",
        "service_charge_hv_steel_companies",
        "demand_charge_hv_steel_companies",
        "energy_ev_charging",
        "service_charge_ev_charging",
        "demand_charge_ev_charging",
    ];

    private readonly IAuthRepository _authRepository;
    private readonly IPasswordSecurityService _passwordSecurityService;
    private readonly ITariffRepository _tariffRepository;

    public AdminController(
        IAuthRepository authRepository,
        IPasswordSecurityService passwordSecurityService,
        ITariffRepository tariffRepository)
    {
        _authRepository = authRepository;
        _passwordSecurityService = passwordSecurityService;
        _tariffRepository = tariffRepository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? successMessage = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var model = await BuildDashboardModelAsync(
            successMessage: successMessage,
            errorMessage: errorMessage,
            tariffUpdate: null,
            cancellationToken: cancellationToken);

        return View(model);
    }

    [HttpPost("create-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(
        string displayName,
        string email,
        string password,
        string roleName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(roleName))
        {
            return RedirectToAction(nameof(Index), new { errorMessage = "Name, email, password and role are required." });
        }

        if (!string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(roleName, "User", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { errorMessage = "Only Admin or User roles are allowed." });
        }

        if (!_passwordSecurityService.TryValidateStrongPassword(password, out var passwordError))
        {
            return RedirectToAction(nameof(Index), new { errorMessage = passwordError });
        }

        var cleanEmail = email.Trim().ToLowerInvariant();
        var existingUser = await _authRepository.GetUserByEmailAsync(cleanEmail, cancellationToken);

        if (existingUser is not null && !existingUser.IsDeleted)
        {
            return RedirectToAction(nameof(Index), new { errorMessage = "A user with this email already exists." });
        }

        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var cleanRole = roleName.Trim();
        var (hash, salt) = _passwordSecurityService.HashPassword(password);

        try
        {
            if (existingUser is not null && existingUser.IsDeleted)
            {
                await _authRepository.ReactivateUserAsync(
                    displayName.Trim(),
                    cleanEmail,
                    hash,
                    salt,
                    cleanRole,
                    cancellationToken);
            }
            else
            {
                await _authRepository.CreateUserAsync(
                    displayName.Trim(),
                    cleanEmail,
                    hash,
                    salt,
                    cleanRole,
                    cancellationToken);
            }
        }
        catch
        {
            return RedirectToAction(nameof(Index), new
            {
                errorMessage = "Unable to create user. This email may already exist in the database."
            });
        }

        await _authRepository.LogAuditAsync(
            actorEmail,
            cleanEmail,
            existingUser is not null && existingUser.IsDeleted ? "REACTIVATE_USER" : "REGISTER",
            existingUser is not null && existingUser.IsDeleted
                ? $"Admin reactivated user with role {cleanRole}."
                : $"Admin created user with role {cleanRole}.",
            cancellationToken);

        return RedirectToAction(nameof(Index), new { successMessage = $"Created user {cleanEmail}." });
    }

    [HttpPost("update-tariff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTariff(
        [Bind(Prefix = "TariffUpdate")] TariffAdminUpdateInput tariffUpdate,
        CancellationToken cancellationToken)
    {
        if (!TryValidateTariffUpdate(tariffUpdate, out var validationMessage))
        {
            var invalidModel = await BuildDashboardModelAsync(
                successMessage: null,
                errorMessage: validationMessage,
                tariffUpdate: tariffUpdate,
                cancellationToken: cancellationToken);

            return View("Index", invalidModel);
        }

        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";

        try
        {
            var result = await _tariffRepository.UpsertTariffEntryAsync(tariffUpdate, cancellationToken);

            await _authRepository.LogAuditAsync(
                actorEmail,
                actorEmail,
                $"UPSERT_{result.RecordKind.ToUpperInvariant()}",
                BuildTariffAuditDetail(result),
                cancellationToken);

            return RedirectToAction(nameof(Index), new
            {
                successMessage = BuildTariffSuccessMessage(result)
            });
        }
        catch (Exception exception)
        {
            var errorModel = await BuildDashboardModelAsync(
                successMessage: null,
                errorMessage: $"Unable to update the tariff database. {exception.Message}",
                tariffUpdate: tariffUpdate,
                cancellationToken: cancellationToken);

            return View("Index", errorModel);
        }
    }

    [HttpPost("update-role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRole(string targetEmail, string roleName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetEmail) || string.IsNullOrWhiteSpace(roleName))
        {
            return RedirectToAction(nameof(Index), new { errorMessage = "Target email and role are required." });
        }

        if (!string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(roleName, "User", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { errorMessage = "Only Admin or User roles are allowed." });
        }

        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        await _authRepository.UpdateRoleAsync(targetEmail.Trim(), roleName.Trim(), cancellationToken);
        await _authRepository.LogAuditAsync(actorEmail, targetEmail.Trim(), "UPDATE_ROLE", $"Role changed to {roleName}.", cancellationToken);

        return RedirectToAction(nameof(Index), new { successMessage = $"Updated role for {targetEmail}." });
    }

    [HttpPost("delete-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string targetEmail, CancellationToken cancellationToken)
    {
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(targetEmail))
        {
            return RedirectToAction(nameof(Index), new { errorMessage = "Target email is required." });
        }

        if (string.Equals(actorEmail, targetEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { errorMessage = "Admin cannot delete the currently signed-in account." });
        }

        await _authRepository.HardDeleteUserAsync(targetEmail.Trim(), cancellationToken);
        await _authRepository.LogAuditAsync(actorEmail, targetEmail.Trim(), "DELETE_USER", "User account was permanently deleted.", cancellationToken);

        return RedirectToAction(nameof(Index), new { successMessage = $"Deleted {targetEmail}." });
    }

    private async Task<AdminDashboardViewModel> BuildDashboardModelAsync(
        string? successMessage,
        string? errorMessage,
        TariffAdminUpdateInput? tariffUpdate,
        CancellationToken cancellationToken)
    {
        var effectiveTariffUpdate = tariffUpdate ?? new TariffAdminUpdateInput();
        if (effectiveTariffUpdate.CalendarYear <= 0)
        {
            effectiveTariffUpdate.CalendarYear = DateTime.UtcNow.Year;
        }

        return new AdminDashboardViewModel
        {
            Users = await _authRepository.GetUsersAsync(cancellationToken),
            AuditLogs = await _authRepository.GetAuditLogsAsync(cancellationToken),
            TariffUpdate = effectiveTariffUpdate,
            TariffCategoryChoices = TariffCategoryChoices,
            ComponentTypeSuggestions = ComponentTypeSuggestions,
            SuccessMessage = successMessage,
            ErrorMessage = errorMessage,
        };
    }

    private static bool TryValidateTariffUpdate(
        TariffAdminUpdateInput tariffUpdate,
        out string errorMessage)
    {
        if (tariffUpdate.CalendarYear is < 1990 or > 2100)
        {
            errorMessage = "Enter a valid calendar year for the tariff update.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(tariffUpdate.Period))
        {
            errorMessage = "Tariff effective period is required.";
            return false;
        }

        try
        {
            var extractedYear = TariffCategoryRules.ExtractCalendarYear(tariffUpdate.Period.Trim());
            if (extractedYear != tariffUpdate.CalendarYear)
            {
                errorMessage = "The effective period must contain the same calendar year entered in the form.";
                return false;
            }
        }
        catch
        {
            errorMessage = "The effective period must include a valid year, for example Q3 2026 (July).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(tariffUpdate.Category))
        {
            errorMessage = "Customer category is required.";
            return false;
        }

        try
        {
            TariffCategoryRules.ResolveDatabaseCategoryId(tariffUpdate.Category.Trim());
        }
        catch
        {
            errorMessage = "Select a supported tariff category before updating the database.";
            return false;
        }

        if (!TariffAdminRecordKinds.IsSupported(tariffUpdate.RecordKind?.Trim()))
        {
            errorMessage = "Choose whether you are updating a tariff component, tax, or levy.";
            return false;
        }

        if (tariffUpdate.Rate < 0m)
        {
            errorMessage = "Rate cannot be negative.";
            return false;
        }

        if (string.Equals(tariffUpdate.RecordKind, TariffAdminRecordKinds.TariffComponent, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(tariffUpdate.ComponentType))
            {
                errorMessage = "Component type is required for tariff component updates.";
                return false;
            }

            if (tariffUpdate.BlockStart < 0 || tariffUpdate.BlockEnd < 0)
            {
                errorMessage = "Block start and block end cannot be negative.";
                return false;
            }

            if (tariffUpdate.BlockStart.HasValue &&
                tariffUpdate.BlockEnd.HasValue &&
                tariffUpdate.BlockEnd.Value < tariffUpdate.BlockStart.Value)
            {
                errorMessage = "Block end must be greater than or equal to block start.";
                return false;
            }
        }
        else if (string.IsNullOrWhiteSpace(tariffUpdate.ChargeName))
        {
            errorMessage = "Enter the tax or levy name before updating the database.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static string BuildTariffSuccessMessage(TariffAdminUpdateResult result)
    {
        var operation = result.Inserted ? "Inserted" : "Updated";
        var recordKind = GetRecordKindLabel(result.RecordKind);
        return $"{operation} {recordKind} '{result.RecordLabel}' for {result.Period} / {result.Category}. The live tariff database has been updated.";
    }

    private static string BuildTariffAuditDetail(TariffAdminUpdateResult result)
    {
        var operation = result.Inserted ? "inserted" : "updated";
        var recordKind = GetRecordKindLabel(result.RecordKind);
        return $"Admin {operation} {recordKind} '{result.RecordLabel}' for {result.Period} / {result.Category} (yearId {result.YearId}).";
    }

    private static string GetRecordKindLabel(string recordKind)
    {
        return recordKind switch
        {
            TariffAdminRecordKinds.TariffComponent => "tariff component",
            TariffAdminRecordKinds.Tax => "tax record",
            TariffAdminRecordKinds.Levy => "levy record",
            _ => "tariff record",
        };
    }
}
