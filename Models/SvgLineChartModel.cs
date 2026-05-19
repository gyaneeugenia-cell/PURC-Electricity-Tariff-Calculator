namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class SvgLineChartModel
{
    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public decimal PlotLeft { get; init; }

    public decimal PlotRight { get; init; }

    public decimal PlotTop { get; init; }

    public decimal PlotBottom { get; init; }

    public decimal MinY { get; init; }

    public decimal MaxY { get; init; }

    public IReadOnlyList<decimal> YTicks { get; init; } = Array.Empty<decimal>();

    public IReadOnlyList<ChartPoint> Points { get; init; } = Array.Empty<ChartPoint>();

    public string PolylinePoints { get; init; } = string.Empty;

    public decimal LabelOffset { get; init; } = 18m;
}
