namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class TariffCalculationResult
{
    public int YearId { get; init; }

    public int CalendarYear { get; init; }

    public string Period { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public decimal ConsumptionKwh { get; init; }

    public decimal TotalBill { get; init; }

    public decimal DemandKva { get; init; }

    public bool IsReverseCalculation { get; init; }

    public string Note { get; init; } = string.Empty;

    public IReadOnlyList<BillBreakdownLine> Breakdown { get; init; } = Array.Empty<BillBreakdownLine>();
}
