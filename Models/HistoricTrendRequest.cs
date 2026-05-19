namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class HistoricTrendRequest
{
    public string Category { get; init; } = string.Empty;

    public decimal ConsumptionKwh { get; init; }

    public IReadOnlyList<int> CalendarYears { get; init; } = Array.Empty<int>();

    public bool UseLatestPeriod { get; init; } = true;

    public Dictionary<int, string> PeriodByYear { get; init; } = new();

    public Dictionary<int, decimal> DemandByYear { get; init; } = new();
}
