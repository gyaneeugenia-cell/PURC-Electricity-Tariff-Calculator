namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class TariffPeriodCatalogItem
{
    public int YearId { get; init; }

    public string Period { get; init; } = string.Empty;

    public int CalendarYear { get; init; }
}
