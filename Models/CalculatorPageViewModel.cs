namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class CalculatorPageViewModel
{
    public IReadOnlyList<TariffPeriodCatalogItem> AllPeriods { get; set; } = Array.Empty<TariffPeriodCatalogItem>();

    public IReadOnlyList<TariffCategoryOption> AvailableCategories { get; set; } = Array.Empty<TariffCategoryOption>();

    public IReadOnlyList<int> AvailableYears { get; set; } = Array.Empty<int>();

    public int SelectedCalendarYear { get; set; }

    public int SelectedYearId { get; set; }

    public string SelectedPeriod { get; set; } = string.Empty;

    public string SelectedCategory { get; set; } = "Residential";

    public string Mode { get; set; } = "Consumption";

    public decimal ConsumptionKwh { get; set; } = 100m;

    public decimal TotalBillInput { get; set; } = 100m;

    public decimal DemandKva { get; set; }

    public bool ShowTrendTools { get; set; }

    public bool UseLatestPeriodForTrend { get; set; } = true;

    public bool ShowTrendTable { get; set; } = true;

    public string TrendCategory { get; set; } = "Residential";

    public decimal TrendConsumptionKwh { get; set; } = 100m;

    public List<int> TrendYears { get; set; } = new();

    public Dictionary<int, string> TrendPeriodByYear { get; set; } = new();

    public Dictionary<int, decimal> TrendDemandByYear { get; set; } = new();

    public TariffCalculationResult? CalculationResult { get; set; }

    public TariffBundle? SelectedTariffBundle { get; set; }

    public DateTimeOffset PageTimestamp { get; set; } = DateTimeOffset.Now;

    public IReadOnlyList<HistoricTrendRow> TrendRows { get; set; } = Array.Empty<HistoricTrendRow>();

    public string? ErrorMessage { get; set; }

    public bool HasResults => CalculationResult is not null;

    public bool HasTrend => TrendRows.Count > 0;
}
