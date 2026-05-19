using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurcHistoricTariffReckoner.CSharp.Models;
using PurcHistoricTariffReckoner.CSharp.Services;

namespace PurcHistoricTariffReckoner.CSharp.Controllers;

[AllowAnonymous]
[Route("account")]
public sealed class AccountController : Controller
{
    private readonly IAuthRepository _authRepository;
    private readonly IPasswordSecurityService _passwordSecurityService;

    public AccountController(
        IAuthRepository authRepository,
        IPasswordSecurityService passwordSecurityService)
    {
        _authRepository = authRepository;
        _passwordSecurityService = passwordSecurityService;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _authRepository.GetUserByEmailAsync(model.Email.Trim(), cancellationToken);
        if (user is null || user.IsDeleted || !_passwordSecurityService.VerifyPassword(model.Password, user.PasswordHash, user.PasswordSalt))
        {
            model.ErrorMessage = "Invalid email or password.";
            return View(model);
        }

        await SignInAsync(user);
        await _authRepository.UpdateLastLoginAsync(user.Email, cancellationToken);
        await _authRepository.LogAuditAsync(user.Email, user.Email, "LOGIN", "User signed in.", cancellationToken);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("register")]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_passwordSecurityService.TryValidateStrongPassword(model.Password, out var passwordError))
        {
            model.ErrorMessage = passwordError;
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var existingUser = await _authRepository.GetUserByEmailAsync(email, cancellationToken);
        if (existingUser is not null && !existingUser.IsDeleted)
        {
            model.ErrorMessage = "An account with this email already exists.";
            return View(model);
        }

        var activeUserCount = await _authRepository.CountActiveUsersAsync(cancellationToken);
        var roleName = activeUserCount == 0 ? "Admin" : "User";
        var (hash, salt) = _passwordSecurityService.HashPassword(model.Password);

        await _authRepository.CreateUserAsync(
            model.DisplayName.Trim(),
            email,
            hash,
            salt,
            roleName,
            cancellationToken);
        await _authRepository.LogAuditAsync(email, email, "REGISTER", $"User registered with role {roleName}.", cancellationToken);

        var createdUser = await _authRepository.GetUserByEmailAsync(email, cancellationToken);
        if (createdUser is not null)
        {
            await SignInAsync(createdUser);
            await _authRepository.UpdateLastLoginAsync(createdUser.Email, cancellationToken);
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(email))
        {
            await _authRepository.LogAuditAsync(email, email, "LOGOUT", "User signed out.", cancellationToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet("change-password")]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost("change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_passwordSecurityService.TryValidateStrongPassword(model.NewPassword, out var passwordError))
        {
            model.ErrorMessage = passwordError;
            return View(model);
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _authRepository.GetUserByEmailAsync(email, cancellationToken);
        if (user is null || user.IsDeleted || !_passwordSecurityService.VerifyPassword(model.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            model.ErrorMessage = "Current password is incorrect.";
            return View(model);
        }

        var (hash, salt) = _passwordSecurityService.HashPassword(model.NewPassword);
        await _authRepository.UpdatePasswordAsync(email, hash, salt, cancellationToken);
        await _authRepository.LogAuditAsync(email, email, "CHANGE_PASSWORD", "Password updated by user.", cancellationToken);

        model.SuccessMessage = "Password changed successfully.";
        return View(model);
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var user = await _authRepository.GetUserByEmailAsync(email, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            model.SuccessMessage = "If the email exists, a reset token will be generated.";
            return View(model);
        }

        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
        var tokenHash = HashResetToken(resetToken);

        await _authRepository.CreateResetTokenAsync(
            email,
            Guid.NewGuid().ToString("N"),
            tokenHash,
            DateTimeOffset.UtcNow.AddMinutes(30),
            cancellationToken);
        await _authRepository.LogAuditAsync(email, email, "FORGOT_PASSWORD", "Password reset token generated.", cancellationToken);

        model.SuccessMessage = "A reset token was generated. Use it on the reset password page within 30 minutes.";
        model.ResetToken = resetToken;
        return View(model);
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword()
    {
        return View(new ResetPasswordViewModel());
    }

    [HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_passwordSecurityService.TryValidateStrongPassword(model.NewPassword, out var passwordError))
        {
            model.ErrorMessage = passwordError;
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var tokenIsValid = await _authRepository.ValidateResetTokenAsync(
            email,
            storedHash => string.Equals(storedHash, HashResetToken(model.ResetToken.Trim()), StringComparison.Ordinal),
            cancellationToken);

        if (!tokenIsValid)
        {
            model.ErrorMessage = "The reset token is invalid or expired.";
            return View(model);
        }

        var (hash, salt) = _passwordSecurityService.HashPassword(model.NewPassword);
        await _authRepository.UpdatePasswordAsync(email, hash, salt, cancellationToken);
        await _authRepository.ConsumeResetTokenAsync(email, cancellationToken);
        await _authRepository.LogAuditAsync(email, email, "RESET_PASSWORD", "Password reset completed with a reset token.", cancellationToken);

        model.SuccessMessage = "Password reset completed. You can now sign in.";
        return View(model);
    }

    private async Task SignInAsync(StoredAppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RoleName),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });
    }

    private static string HashResetToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
