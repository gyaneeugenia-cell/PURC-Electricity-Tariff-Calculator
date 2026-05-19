namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class TaxRateRecord
{
    public string TaxName { get; init; } = string.Empty;

    public decimal TaxRate { get; init; }
}
