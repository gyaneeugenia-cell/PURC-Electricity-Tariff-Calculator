namespace PurcHistoricTariffReckoner.CSharp.Services;

public interface IPasswordSecurityService
{
    (string Hash, string Salt) HashPassword(string password);

    bool VerifyPassword(string password, string storedHash, string storedSalt);

    bool TryValidateStrongPassword(string password, out string errorMessage);
}
