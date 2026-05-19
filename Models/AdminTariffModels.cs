namespace PurcHistoricTariffReckoner.CSharp.Models;

public static class TariffAdminRecordKinds
{
    public const string TariffComponent = "TariffComponent";
    public const string Tax = "Tax";
    public const string Levy = "Levy";

    public static bool IsSupported(string? recordKind)
    {
        return recordKind is TariffComponent or Tax or Levy;
    }
}

public sealed class TariffAdminUpdateInput
{
    public int CalendarYear { get; set; } = DateTime.UtcNow.Year;

    public string Period { get; set; } = string.Empty;

    public string Category { get; set; } = "Residential";

    public string RecordKind { get; set; } = TariffAdminRecordKinds.TariffComponent;

    public string ComponentType { get; set; } = "energy";

    public string ChargeName { get; set; } = "Tax";

    public decimal Rate { get; set; }

    public int? BlockStart { get; set; }

    public int? BlockEnd { get; set; }

    public string? Unit { get; set; }

    public string? Notes { get; set; }
}

public sealed class TariffAdminUpdateResult
{
    public int YearId { get; init; }

    public int CalendarYear { get; init; }

    public string Period { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string RecordKind { get; init; } = string.Empty;

    public string RecordLabel { get; init; } = string.Empty;

    public bool Inserted { get; init; }
}
