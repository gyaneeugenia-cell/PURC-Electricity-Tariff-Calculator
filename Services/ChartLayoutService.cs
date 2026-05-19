using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public static class ChartLayoutService
{
    public static SvgLineChartModel BuildBillTrendChart(IReadOnlyList<HistoricTrendRow> rows)
    {
        const decimal width = 700m;
        const decimal height = 300m;
        const decimal plotLeft = 64m;
        const decimal plotRight = 24m;
        const decimal plotTop = 16m;
        const decimal plotBottom = 54m;

        if (rows.Count == 0)
        {
            return new SvgLineChartModel
            {
                Width = width,
                Height = height,
                PlotLeft = plotLeft,
                PlotRight = plotRight,
                PlotTop = plotTop,
                PlotBottom = plotBottom,
            };
        }

        var values = rows.Select(row => row.TotalBill).ToList();
        var (minY, maxY, yTicks) = BuildNiceAxis(values, 5);
        var plotWidth = width - plotLeft - plotRight;
        var plotHeight = height - plotTop - plotBottom;
        var count = rows.Count;
        var xStep = count == 1 ? 0m : plotWidth / (count - 1);

        decimal ProjectY(decimal value)
        {
            if (maxY == minY)
            {
                return plotTop + (plotHeight / 2m);
            }

            var normalized = (value - minY) / (maxY - minY);
            return plotTop + plotHeight - (normalized * plotHeight);
        }

        var points = new List<ChartPoint>();

        for (var index = 0; index < rows.Count; index++)
        {
            var x = plotLeft + (index * xStep);
            var y = ProjectY(rows[index].TotalBill);
            points.Add(new ChartPoint(x, y, rows[index].CalendarYear.ToString(), rows[index].TotalBill));
        }

        var polyline = string.Join(" ", points.Select(point => $"{point.X:0.##},{point.Y:0.##}"));

        return new SvgLineChartModel
        {
            Width = width,
            Height = height,
            PlotLeft = plotLeft,
            PlotRight = plotRight,
            PlotTop = plotTop,
            PlotBottom = plotBottom,
            MinY = minY,
            MaxY = maxY,
            YTicks = yTicks,
            Points = points,
            PolylinePoints = polyline,
        };
    }

    private static (decimal minY, decimal maxY, IReadOnlyList<decimal> ticks) BuildNiceAxis(
        IReadOnlyList<decimal> values,
        int maxTicks)
    {
        var min = values.Min();
        var max = values.Max();

        if (min == max)
        {
            var fallbackStep = NextNiceStep(Math.Max(1m, Math.Abs(max) * 0.1m));
            return (Math.Max(0m, min - fallbackStep), max + fallbackStep, new[] { min, max + fallbackStep });
        }

        var step = NextNiceStep((max - min) / Math.Max(1, maxTicks - 1));
        decimal first;
        decimal last;
        int tickCount;

        while (true)
        {
            first = Math.Floor(min / step) * step;
            last = Math.Ceiling(max / step) * step;
            tickCount = (int)((last - first) / step) + 1;

            if (tickCount <= maxTicks)
            {
                break;
            }

            step = NextNiceStep(step * 1.01m);
        }

        var ticks = new List<decimal>();
        var value = first;

        while (value <= last + (step * 0.001m))
        {
            ticks.Add(decimal.Round(value, 2, MidpointRounding.AwayFromZero));
            value += step;
        }

        var minY = Math.Max(0m, first - step);
        var maxY = last + (step * 0.12m);
        return (minY, maxY, ticks);
    }

    private static decimal NextNiceStep(decimal step)
    {
        if (step <= 0m)
        {
            return 1m;
        }

        var bases = new[] { 1m, 1.5m, 2m, 2.5m, 5m, 10m };
        var exponent = (int)Math.Floor(Math.Log10((double)step));
        var scale = (decimal)Math.Pow(10d, exponent);
        var fraction = step / scale;

        foreach (var basis in bases)
        {
            if (fraction < basis)
            {
                return basis * scale;
            }
        }

        return 10m * scale;
    }
}
