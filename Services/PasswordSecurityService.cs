using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public sealed class PasswordSecurityService : IPasswordSecurityService
{
    private const int IterationCount = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            IterationCount,
            HashAlgorithmName.SHA512,
            HashSize);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        var expectedHash = Convert.FromBase64String(storedHash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            IterationCount,
            HashAlgorithmName.SHA512,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public bool TryValidateStrongPassword(string password, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            errorMessage = "Password must be at least 12 characters long.";
            return false;
        }

        if (!Regex.IsMatch(password, "[A-Z]"))
        {
            errorMessage = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (!Regex.IsMatch(password, "[a-z]"))
        {
            errorMessage = "Password must contain at least one lowercase letter.";
            return false;
        }

        if (!Regex.IsMatch(password, "[0-9]"))
        {
            errorMessage = "Password must contain at least one number.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[^A-Za-z0-9]"))
        {
            errorMessage = "Password must contain at least one special character.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
