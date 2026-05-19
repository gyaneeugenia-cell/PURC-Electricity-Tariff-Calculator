namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class TariffBundle
{
    public IReadOnlyList<TariffComponent> Components { get; init; } = Array.Empty<TariffComponent>();

    public TaxRateRecord? Tax { get; init; }

    public IReadOnlyList<LevyRateRecord> Levies { get; init; } = Array.Empty<LevyRateRecord>();
}
