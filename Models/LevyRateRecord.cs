namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class LevyRateRecord
{
    public string LevyName { get; init; } = string.Empty;

    public decimal LevyRate { get; init; }
}
