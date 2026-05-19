using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurcHistoricTariffReckoner.CSharp.Models;
using PurcHistoricTariffReckoner.CSharp.Services;

namespace PurcHistoricTariffReckoner.CSharp.Controllers;

[Authorize]
[ApiController]
[Route("api/trends")]
public sealed class TrendController : ControllerBase
{
    private readonly ITariffCalculationService _calculationService;

    public TrendController(ITariffCalculationService calculationService)
    {
        _calculationService = calculationService;
    }

    [HttpPost("historic")]
    public async Task<ActionResult<IReadOnlyList<HistoricTrendRow>>> BuildHistoricTrend(
        [FromBody] HistoricTrendRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await _calculationService.BuildHistoricTrendAsync(request, cancellationToken);
        return Ok(rows);
    }
}
