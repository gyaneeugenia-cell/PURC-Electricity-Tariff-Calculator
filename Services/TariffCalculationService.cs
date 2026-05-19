using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public sealed class TariffCalculationService : ITariffCalculationService
{
    private readonly ITariffRepository _repository;
    private readonly IConfiguration _configuration;

    public TariffCalculationService(ITariffRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public async Task<TariffCalculationResult> CalculateAsync(
        TariffCalculationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ConsumptionKwh is null)
        {
            throw new InvalidOperationException("ConsumptionKwh is required for forward calculation.");
        }

        var context = await BuildContextAsync(request, cancellationToken);
        return CalculateFromContext(context, request.ConsumptionKwh.Value, false);
    }

    public async Task<TariffCalculationResult> ReverseCalculateAsync(
        TariffCalculationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TotalBill is null)
        {
            throw new InvalidOperationException("TotalBill is required for reverse calculation.");
        }

        var context = await BuildContextAsync(request, cancellationToken);
        var target = Money(request.TotalBill.Value);
        var reverseSearchMaxKwh = _configuration.GetValue("TariffEngine:ReverseSearchMaxKwh", 10000);

        decimal bestKwh = 0m;
        decimal bestDiff = decimal.MaxValue;

        for (var kwh = 0; kwh <= reverseSearchMaxKwh; kwh++)
        {
            var candidate = CalculateFromContext(context, kwh, false);
            var diff = Math.Abs(candidate.TotalBill - target);

            if (diff < bestDiff || (diff == bestDiff && kwh < bestKwh))
            {
                bestDiff = diff;
                bestKwh = kwh;
            }

            if (candidate.TotalBill == target)
            {
                return CalculateFromContext(context, bestKwh, true);
            }
        }

        var refinementLow = Math.Max(0m, bestKwh - 1m);
        var refinementHigh = Math.Min(reverseSearchMaxKwh, bestKwh + 1m);

        for (var kwh = refinementLow; kwh <= refinementHigh; kwh += 0.01m)
        {
            var candidateKwh = decimal.Round(kwh, 2, MidpointRounding.AwayFromZero);
            var candidate = CalculateFromContext(context, candidateKwh, false);
            var diff = Math.Abs(candidate.TotalBill - target);

            if (diff < bestDiff || (diff == bestDiff && candidateKwh < bestKwh))
            {
                bestDiff = diff;
                bestKwh = candidateKwh;
            }

            if (candidate.TotalBill == target)
            {
                bestKwh = candidateKwh;
                break;
            }
        }

        return CalculateFromContext(context, bestKwh, true);
    }

    public async Task<IReadOnlyList<HistoricTrendRow>> BuildHistoricTrendAsync(
        HistoricTrendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CalendarYears.Count == 0)
        {
            return Array.Empty<HistoricTrendRow>();
        }

        var catalog = await GetPeriodCatalogAsync(cancellationToken);
        var rows = new List<HistoricTrendRow>();

        foreach (var calendarYear in request.CalendarYears.Distinct().OrderBy(year => year))
        {
            var periodsForYear = catalog
                .Where(item => item.CalendarYear == calendarYear)
                .OrderBy(item => item.YearId)
                .ToList();

            if (periodsForYear.Count == 0)
            {
                continue;
            }

            TariffPeriodCatalogItem selectedPeriod;

            if (request.PeriodByYear.TryGetValue(calendarYear, out var requestedPeriod) &&
                !string.IsNullOrWhiteSpace(requestedPeriod))
            {
                selectedPeriod = periodsForYear
                    .FirstOrDefault(item => string.Equals(item.Period, requestedPeriod, StringComparison.OrdinalIgnoreCase))
                    ?? periodsForYear[^1];
            }
            else
            {
                selectedPeriod = request.UseLatestPeriod
                    ? periodsForYear[^1]
                    : periodsForYear[0];
            }

            request.DemandByYear.TryGetValue(calendarYear, out var demandKva);

            var calculation = await CalculateAsync(
                new TariffCalculationRequest
                {
                    YearId = selectedPeriod.YearId,
                    Period = selectedPeriod.Period,
                    Category = request.Category,
                    ConsumptionKwh = request.ConsumptionKwh,
                    DemandKva = demandKva,
                },
                cancellationToken);

            rows.Add(new HistoricTrendRow
            {
                YearId = calculation.YearId,
                CalendarYear = calculation.CalendarYear,
                Period = calculation.Period,
                Category = calculation.Category,
                ConsumptionKwh = calculation.ConsumptionKwh,
                DemandKva = demandKva > 0 ? demandKva : null,
                TotalBill = calculation.TotalBill,
            });
        }

        return rows;
    }

    private async Task<CalculationContext> BuildContextAsync(
        TariffCalculationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Period))
        {
            throw new InvalidOperationException("Period is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("Category is required.");
        }

        var categoryId = TariffCategoryRules.ResolveDatabaseCategoryId(request.Category);
        var yearId = request.YearId is > 0
            ? request.YearId.Value
            : await ResolveYearIdAsync(request.Period, cancellationToken);
        var calendarYear = TariffCategoryRules.ExtractCalendarYear(request.Period);
        var bundle = await _repository.GetTariffBundleAsync(yearId, request.Period, categoryId, cancellationToken);

        if (!bundle.Components.Any())
        {
            throw new InvalidOperationException(
                $"No tariff components were found for yearId={yearId}, period='{request.Period}', categoryId={categoryId}.");
        }

        var demandKva = request.DemandKva ?? 0m;
        if (TariffCategoryRules.RequiresSltDemand(request.Category, calendarYear, request.Period) && demandKva <= 0m)
        {
            throw new InvalidOperationException("Maximum demand must be greater than zero for the selected SLT tariff.");
        }

        return new CalculationContext(
            yearId,
            calendarYear,
            request.Period,
            request.Category,
            categoryId,
            demandKva,
            bundle);
    }

    private async Task<int> ResolveYearIdAsync(string period, CancellationToken cancellationToken)
    {
        var exactMatch = (await GetPeriodCatalogAsync(cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Period, period, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null && exactMatch.YearId > 0)
        {
            return exactMatch.YearId;
        }

        return await _repository.ResolveYearIdByPeriodAsync(period, cancellationToken);
    }

    private TariffCalculationResult CalculateFromContext(CalculationContext context, decimal consumptionKwh, bool isReverseCalculation)
    {
        if (consumptionKwh < 0)
        {
            throw new InvalidOperationException("Consumption cannot be negative.");
        }

        IReadOnlyList<BillBreakdownLine> lines = context.CategoryId switch
        {
            1 => CalculateResidential(context, consumptionKwh),
            2 => CalculateNonResidential(context, consumptionKwh),
            3 => CalculateSlt(context, consumptionKwh),
            4 => CalculateSlt(context, consumptionKwh),
            _ => throw new InvalidOperationException($"Unsupported database category id {context.CategoryId}."),
        };

        var totalBill = Money(lines.Sum(line => line.Amount));
        var breakdown = lines
            .Append(new BillBreakdownLine("TOTAL BILL", totalBill))
            .ToArray();

        var note = $"C# PostgreSQL calculation for {context.Period} {context.Category}.";

        return new TariffCalculationResult
        {
            YearId = context.YearId,
            CalendarYear = context.CalendarYear,
            Period = context.Period,
            Category = context.Category,
            ConsumptionKwh = consumptionKwh,
            DemandKva = context.DemandKva,
            TotalBill = totalBill,
            IsReverseCalculation = isReverseCalculation,
            Note = note,
            Breakdown = breakdown,
        };
    }

    private IReadOnlyList<BillBreakdownLine> CalculateResidential(CalculationContext context, decimal consumptionKwh)
    {
        var components = context.Bundle.Components;
        var fixedCharge = components.FirstOrDefault(item => item.ComponentType == "fixed_charge");

        if (fixedCharge is not null)
        {
            return CalculateFixedChargeResidential(context, consumptionKwh, fixedCharge.Rate);
        }

        var serviceCharge = components
            .Where(item => item.ComponentType == "service_charge")
            .Select(item => item.Rate)
            .FirstOrDefault();

        var lifelineServiceCharge = components
            .Where(item => item.ComponentType == "service_charge_lifeline")
            .Select(item => item.Rate)
            .FirstOrDefault();

        var energyBlocks = components
            .Where(item => item.ComponentType == "energy")
            .OrderBy(item => item.BlockEnd ?? int.MaxValue)
            .ToList();

        var lifelineBlock = energyBlocks
            .Where(item => item.BlockStart == 0 && item.BlockEnd.HasValue)
            .OrderBy(item => item.BlockEnd!.Value)
            .FirstOrDefault();

        if (lifelineBlock is null)
        {
            throw new InvalidOperationException("Residential tariff does not have a lifeline energy block.");
        }

        decimal appliedServiceCharge;
        decimal energyChargeRaw;

        if (consumptionKwh <= lifelineBlock.BlockEnd!.Value)
        {
            appliedServiceCharge = lifelineServiceCharge;
            energyChargeRaw = consumptionKwh * lifelineBlock.Rate;
        }
        else
        {
            appliedServiceCharge = serviceCharge;
            var remainingBands = energyBlocks
                .Where(item => item != lifelineBlock)
                .OrderBy(item => item.BlockEnd ?? int.MaxValue)
                .ToList();

            if (remainingBands.Count == 0)
            {
                throw new InvalidOperationException("Residential tariff is missing the non-lifeline bands.");
            }

            energyChargeRaw = CalculateResidentialBandEnergy(consumptionKwh, remainingBands);
        }

        var leviesRaw = CalculateLeviesRaw(context, consumptionKwh, energyChargeRaw);
        var taxRate = context.Bundle.Tax?.TaxRate ?? 0m;
        var residentialTaxApplies = context.CalendarYear == 1998 || context.CalendarYear >= 2015;
        var taxRaw = residentialTaxApplies
            ? (appliedServiceCharge + energyChargeRaw) * taxRate
            : 0m;

        return BuildBreakdown(
            new[]
            {
                new BillBreakdownLine("Service charge", Money(appliedServiceCharge)),
                new BillBreakdownLine("Energy charge", Money(energyChargeRaw)),
                new BillBreakdownLine("Levies", Money(leviesRaw)),
                new BillBreakdownLine("Tax", Money(taxRaw)),
            });
    }

    private IReadOnlyList<BillBreakdownLine> CalculateFixedChargeResidential(
        CalculationContext context,
        decimal consumptionKwh,
        decimal fixedChargeRate)
    {
        var energyBlocks = context.Bundle.Components
            .Where(item => item.ComponentType == "energy")
            .OrderBy(item => item.BlockStart ?? 0)
            .ToList();

        if (energyBlocks.Count == 0)
        {
            throw new InvalidOperationException("Residential fixed-charge tariff is missing energy blocks.");
        }

        var threshold = Math.Max(0, (energyBlocks.Min(item => item.BlockStart ?? 0) - 1));
        var energyChargeRaw = consumptionKwh > threshold
            ? CalculateIncrementalEnergy(consumptionKwh, energyBlocks, zeroStartIsAbsolute: false)
            : 0m;

        var leviesRaw = CalculateLeviesRaw(context, consumptionKwh, energyChargeRaw);
        var taxRate = context.Bundle.Tax?.TaxRate ?? 0m;
        var residentialTaxApplies = context.CalendarYear == 1998 || context.CalendarYear >= 2015;
        var taxRaw = residentialTaxApplies
            ? (fixedChargeRate + energyChargeRaw) * taxRate
            : 0m;

        return BuildBreakdown(
            new[]
            {
                new BillBreakdownLine("Fixed charge", Money(fixedChargeRate)),
                new BillBreakdownLine("Energy charge", Money(energyChargeRaw)),
                new BillBreakdownLine("Levies", Money(leviesRaw)),
                new BillBreakdownLine("Tax", Money(taxRaw)),
            });
    }

    private IReadOnlyList<BillBreakdownLine> CalculateNonResidential(CalculationContext context, decimal consumptionKwh)
    {
        var serviceCharge = context.Bundle.Components
            .Where(item => item.ComponentType == "service_charge")
            .Select(item => item.Rate)
            .FirstOrDefault();

        var energyBlocks = context.Bundle.Components
            .Where(item => item.ComponentType == "energy")
            .OrderBy(item => item.BlockStart ?? 0)
            .ToList();

        var energyChargeRaw = CalculateIncrementalEnergy(consumptionKwh, energyBlocks, zeroStartIsAbsolute: true);
        var leviesRaw = CalculateLeviesRaw(context, consumptionKwh, energyChargeRaw);
        var taxRate = context.Bundle.Tax?.TaxRate ?? 0m;
        var taxRaw = (serviceCharge + energyChargeRaw) * taxRate;

        return BuildBreakdown(
            new[]
            {
                new BillBreakdownLine("Service charge", Money(serviceCharge)),
                new BillBreakdownLine("Energy charge", Money(energyChargeRaw)),
                new BillBreakdownLine("Levies", Money(leviesRaw)),
                new BillBreakdownLine("Tax", Money(taxRaw)),
            });
    }

    private IReadOnlyList<BillBreakdownLine> CalculateSlt(CalculationContext context, decimal consumptionKwh)
    {
        var level = TariffCategoryRules.GetRateLevel(context.Category);
        var serviceCharge = context.Bundle.Components
            .Where(item => string.Equals(item.ComponentType, $"service_charge_{level}", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Rate)
            .FirstOrDefault();

        var demandChargeRate = context.Bundle.Components
            .Where(item => string.Equals(item.ComponentType, $"demand_charge_{level}", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Rate)
            .FirstOrDefault();

        var energyRate = context.Bundle.Components
            .Where(item => string.Equals(item.ComponentType, $"energy_{level}", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Rate)
            .FirstOrDefault();

        var maxDemandChargeRaw = context.DemandKva * demandChargeRate;
        var energyChargeRaw = consumptionKwh * energyRate;
        var leviesRaw = CalculateLeviesRaw(context, consumptionKwh, energyChargeRaw);
        var taxRate = context.Bundle.Tax?.TaxRate ?? 0m;
        var taxRaw = (serviceCharge + energyChargeRaw) * taxRate;

        return BuildBreakdown(
            new[]
            {
                new BillBreakdownLine("Service charge", Money(serviceCharge)),
                new BillBreakdownLine("Max demand charge", Money(maxDemandChargeRaw)),
                new BillBreakdownLine("Energy charge", Money(energyChargeRaw)),
                new BillBreakdownLine("Levies", Money(leviesRaw)),
                new BillBreakdownLine("Tax", Money(taxRaw)),
            });
    }

    private static decimal CalculateResidentialBandEnergy(decimal consumptionKwh, IReadOnlyList<TariffComponent> bands)
    {
        if (bands.Count == 0)
        {
            return 0m;
        }

        if (!bands[0].BlockEnd.HasValue)
        {
            return consumptionKwh * bands[0].Rate;
        }

        var firstCutoff = bands[0].BlockEnd!.Value;
        if (consumptionKwh <= firstCutoff)
        {
            return consumptionKwh * bands[0].Rate;
        }

        decimal energyChargeRaw = firstCutoff * bands[0].Rate;
        var previousCutoff = firstCutoff;

        for (var index = 1; index < bands.Count; index++)
        {
            var band = bands[index];
            if (!band.BlockEnd.HasValue || consumptionKwh <= band.BlockEnd.Value)
            {
                energyChargeRaw += (consumptionKwh - previousCutoff) * band.Rate;
                return energyChargeRaw;
            }

            energyChargeRaw += (band.BlockEnd.Value - previousCutoff) * band.Rate;
            previousCutoff = band.BlockEnd.Value;
        }

        return energyChargeRaw;
    }

    private static decimal CalculateIncrementalEnergy(
        decimal consumptionKwh,
        IReadOnlyList<TariffComponent> blocks,
        bool zeroStartIsAbsolute)
    {
        decimal energyChargeRaw = 0m;

        foreach (var block in blocks)
        {
            var start = block.BlockStart ?? 0;
            var end = block.BlockEnd;

            if (end is null)
            {
                if (consumptionKwh >= start)
                {
                    var openEndedUnits = consumptionKwh - start + 1;
                    energyChargeRaw += openEndedUnits * block.Rate;
                }

                continue;
            }

            if (consumptionKwh < start)
            {
                continue;
            }

            var upper = Math.Min(consumptionKwh, end.Value);
            decimal units;

            if (zeroStartIsAbsolute && start == 0)
            {
                units = upper - start;
            }
            else
            {
                units = upper - start + 1;
            }

            if (units > 0)
            {
                energyChargeRaw += units * block.Rate;
            }
        }

        return energyChargeRaw;
    }

    private static decimal CalculateLeviesRaw(CalculationContext context, decimal consumptionKwh, decimal energyChargeRaw)
    {
        var levyRateTotal = context.Bundle.Levies.Sum(item => item.LevyRate);

        if (context.CalendarYear <= 1998)
        {
            return consumptionKwh * levyRateTotal;
        }

        return energyChargeRaw * levyRateTotal;
    }

    private static IReadOnlyList<BillBreakdownLine> BuildBreakdown(IEnumerable<BillBreakdownLine> lines)
    {
        return lines
            .Where(line => line.Amount != 0m || line.Label == "Tax")
            .ToArray();
    }

    private static decimal Money(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<IReadOnlyList<TariffPeriodCatalogItem>> GetPeriodCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            var repositoryCatalog = await _repository.GetPeriodCatalogAsync(cancellationToken);
            if (repositoryCatalog.Count > 0)
            {
                return repositoryCatalog;
            }
        }
        catch
        {
        }

        return TariffPeriodCatalog.GetAll();
    }

    private sealed record CalculationContext(
        int YearId,
        int CalendarYear,
        string Period,
        string Category,
        int CategoryId,
        decimal DemandKva,
        TariffBundle Bundle);
}
