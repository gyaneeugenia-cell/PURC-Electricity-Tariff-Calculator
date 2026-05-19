using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurcHistoricTariffReckoner.CSharp.Models;
using PurcHistoricTariffReckoner.CSharp.Services;

namespace PurcHistoricTariffReckoner.CSharp.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly ITariffRepository _repository;

    public CatalogController(ITariffRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("periods")]
    public async Task<ActionResult<IReadOnlyList<TariffPeriodCatalogItem>>> GetPeriods(CancellationToken cancellationToken)
    {
        try
        {
            var periods = await _repository.GetPeriodCatalogAsync(cancellationToken);
            if (periods.Count > 0)
            {
                return Ok(periods);
            }
        }
        catch
        {
        }

        return Ok(TariffPeriodCatalog.GetAll());
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<TariffCategoryOption>>> GetCategories(
        [FromQuery] int yearId,
        [FromQuery] string period,
        CancellationToken cancellationToken)
    {
        if (yearId > 0 && !string.IsNullOrWhiteSpace(period))
        {
            try
            {
                var componentTypes = await _repository.GetCategoryComponentTypesAsync(yearId, period, cancellationToken);
                var databaseCategories = TariffCategoryRules.BuildCategoryOptions(componentTypes);
                if (databaseCategories.Count > 0)
                {
                    return Ok(databaseCategories);
                }
            }
            catch
            {
            }
        }

        var calendarYear = TariffCategoryRules.ExtractCalendarYear(period);
        var categories = TariffCategoryRules.BuildUiCategoryOptions(calendarYear, period);
        return Ok(categories);
    }
}
