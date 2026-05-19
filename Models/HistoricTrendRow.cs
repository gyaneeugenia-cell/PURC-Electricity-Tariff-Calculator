namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class HistoricTrendRow
{
    public int YearId { get; init; }

    public int CalendarYear { get; init; }

    public string Period { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public decimal ConsumptionKwh { get; init; }

    public decimal? DemandKva { get; init; }

    public decimal TotalBill { get; init; }
}
