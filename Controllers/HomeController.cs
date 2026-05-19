using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurcHistoricTariffReckoner.CSharp.Models;
using PurcHistoricTariffReckoner.CSharp.Services;

namespace PurcHistoricTariffReckoner.CSharp.Controllers;

[Authorize]
[Route("")]
public sealed class HomeController : Controller
{
    private readonly ITariffCalculationService _calculationService;
    private readonly ITariffRepository _repository;

    public HomeController(
        ITariffCalculationService calculationService,
        ITariffRepository repository)
    {
        _calculationService = calculationService;
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CalculatorPageViewModel model, CancellationToken cancellationToken)
    {
        model = await BuildPageModelAsync(model, cancellationToken);
        ModelState.Clear();
        return View(model);
    }

    [HttpGet("trend")]
    public async Task<IActionResult> TrendIndex([FromQuery] CalculatorPageViewModel model, CancellationToken cancellationToken)
    {
        model.ShowTrendTools = true;
        model = await BuildPageModelAsync(model, cancellationToken);
        ModelState.Clear();
        return View("Index", model);
    }

    [HttpPost("calculate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(CalculatorPageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model = await BuildPageModelAsync(model, cancellationToken);

            if (string.Equals(model.Mode, "TotalBill", StringComparison.OrdinalIgnoreCase))
            {
                model.CalculationResult = await _calculationService.ReverseCalculateAsync(
                    new TariffCalculationRequest
                    {
                        YearId = model.SelectedYearId,
                        Period = model.SelectedPeriod,
                        Category = model.SelectedCategory,
                        TotalBill = model.TotalBillInput,
                        DemandKva = model.DemandKva,
                        ReverseMode = true,
                    },
                    cancellationToken);
            }
            else
            {
                model.CalculationResult = await _calculationService.CalculateAsync(
                    new TariffCalculationRequest
                    {
                        YearId = model.SelectedYearId,
                        Period = model.SelectedPeriod,
                        Category = model.SelectedCategory,
                        ConsumptionKwh = model.ConsumptionKwh,
                        DemandKva = model.DemandKva,
                    },
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            model = await BuildPageModelAsync(model, cancellationToken);
            model.ErrorMessage = exception.Message;
        }

        ModelState.Clear();
        return View("Index", model);
    }

    [HttpPost("trend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trend(CalculatorPageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model = await BuildPageModelAsync(model, cancellationToken);

            model.TrendRows = await _calculationService.BuildHistoricTrendAsync(
                new HistoricTrendRequest
                {
                    Category = model.TrendCategory,
                    ConsumptionKwh = model.TrendConsumptionKwh,
                    CalendarYears = model.TrendYears,
                    UseLatestPeriod = model.UseLatestPeriodForTrend,
                    PeriodByYear = model.TrendPeriodByYear,
                    DemandByYear = model.TrendDemandByYear,
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            model = await BuildPageModelAsync(model, cancellationToken);
            model.ErrorMessage = exception.Message;
        }

        model.ShowTrendTools = true;
        ModelState.Clear();
        return View("Index", model);
    }

    private async Task<CalculatorPageViewModel> BuildPageModelAsync(
        CalculatorPageViewModel model,
        CancellationToken cancellationToken)
    {
        var orderedPeriods = await GetAvailablePeriodsAsync(cancellationToken);
        var years = orderedPeriods
            .Select(item => item.CalendarYear)
            .Distinct()
            .OrderBy(year => year)
            .ToArray();

        if (model.SelectedCalendarYear == 0 || !years.Contains(model.SelectedCalendarYear))
        {
            model.SelectedCalendarYear = years.LastOrDefault();
        }

        model.ConsumptionKwh = Math.Max(0m, model.ConsumptionKwh);
        model.TotalBillInput = Math.Max(0m, model.TotalBillInput);
        model.DemandKva = Math.Max(0m, model.DemandKva);
        model.TrendConsumptionKwh = Math.Max(0m, model.TrendConsumptionKwh);

        model.TrendDemandByYear = model.TrendDemandByYear
            .ToDictionary(
                entry => entry.Key,
                entry => Math.Max(0m, entry.Value));

        var periodsForSelectedYear = orderedPeriods
            .Where(item => item.CalendarYear == model.SelectedCalendarYear)
            .OrderBy(item => item.YearId)
            .ToList();

        var selectedPeriodItem = periodsForSelectedYear
            .FirstOrDefault(item => string.Equals(item.Period, model.SelectedPeriod, StringComparison.OrdinalIgnoreCase))
            ?? periodsForSelectedYear.FirstOrDefault()
            ?? orderedPeriods.Last();

        model.SelectedCalendarYear = selectedPeriodItem.CalendarYear;
        model.SelectedYearId = selectedPeriodItem.YearId;
        model.SelectedPeriod = selectedPeriodItem.Period;
        model.AllPeriods = orderedPeriods;
        model.AvailableYears = years;
        model.AvailableCategories = await GetAvailableCategoriesAsync(
            model.SelectedYearId,
            model.SelectedPeriod,
            model.SelectedCalendarYear,
            cancellationToken);

        if (!model.AvailableCategories.Any(item => item.Code == model.SelectedCategory))
        {
            model.SelectedCategory = model.AvailableCategories.FirstOrDefault()?.Code ?? "Residential";
        }

        var trendCompatibleCategories = model.AvailableCategories
            .Where(item => item.Code == "Residential" || item.Code == "Non-Residential" || item.Code.StartsWith("SLT"))
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(model.TrendCategory) || !trendCompatibleCategories.Contains(model.TrendCategory))
        {
            model.TrendCategory = trendCompatibleCategories.FirstOrDefault() ?? "Residential";
        }

        model.TrendYears = model.TrendYears
            .Where(years.Contains)
            .Distinct()
            .OrderBy(year => year)
            .ToList();

        if (model.TrendYears.Count == 0)
        {
            model.TrendYears = years.TakeLast(3).ToList();
        }

        var validTrendPeriods = new Dictionary<int, string>();

        foreach (var trendYear in model.TrendYears)
        {
            var periodsForTrendYear = orderedPeriods
                .Where(item => item.CalendarYear == trendYear)
                .OrderBy(item => item.YearId)
                .ToList();

            if (periodsForTrendYear.Count == 0)
            {
                continue;
            }

            if (model.TrendPeriodByYear.TryGetValue(trendYear, out var selectedTrendPeriod) &&
                periodsForTrendYear.Any(item => string.Equals(item.Period, selectedTrendPeriod, StringComparison.OrdinalIgnoreCase)))
            {
                validTrendPeriods[trendYear] = selectedTrendPeriod;
            }
            else
            {
                validTrendPeriods[trendYear] = periodsForTrendYear[^1].Period;
            }
        }

        model.TrendPeriodByYear = validTrendPeriods;
        model.PageTimestamp = DateTimeOffset.Now;

        try
        {
            var selectedCategoryId = TariffCategoryRules.ResolveDatabaseCategoryId(model.SelectedCategory);
            model.SelectedTariffBundle = await _repository.GetTariffBundleAsync(
                model.SelectedYearId,
                model.SelectedPeriod,
                selectedCategoryId,
                cancellationToken);
        }
        catch
        {
            model.SelectedTariffBundle = null;
        }

        return await Task.FromResult(model);
    }

    private async Task<IReadOnlyList<TariffPeriodCatalogItem>> GetAvailablePeriodsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var repositoryPeriods = await _repository.GetPeriodCatalogAsync(cancellationToken);
            if (repositoryPeriods.Count > 0)
            {
                return repositoryPeriods
                    .OrderBy(item => item.CalendarYear)
                    .ThenBy(item => item.YearId)
                    .ThenBy(item => item.Period, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
        catch
        {
        }

        return TariffPeriodCatalog.GetAll();
    }

    private async Task<IReadOnlyList<TariffCategoryOption>> GetAvailableCategoriesAsync(
        int yearId,
        string period,
        int calendarYear,
        CancellationToken cancellationToken)
    {
        try
        {
            var componentTypes = await _repository.GetCategoryComponentTypesAsync(yearId, period, cancellationToken);
            var categories = TariffCategoryRules.BuildCategoryOptions(componentTypes);
            if (categories.Count > 0)
            {
                return categories;
            }
        }
        catch
        {
        }

        return TariffCategoryRules.BuildUiCategoryOptions(calendarYear, period);
    }
}
