namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class TariffComponent
{
    public string ComponentType { get; init; } = string.Empty;

    public int? BlockStart { get; init; }

    public int? BlockEnd { get; init; }

    public decimal Rate { get; init; }

    public string? Unit { get; init; }

    public string? Notes { get; init; }
}
