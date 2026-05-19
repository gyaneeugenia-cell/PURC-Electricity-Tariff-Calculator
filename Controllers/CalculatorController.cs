using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurcHistoricTariffReckoner.CSharp.Models;
using PurcHistoricTariffReckoner.CSharp.Services;

namespace PurcHistoricTariffReckoner.CSharp.Controllers;

[Authorize]
[ApiController]
[Route("api/calculator")]
public sealed class CalculatorController : ControllerBase
{
    private readonly ITariffCalculationService _calculationService;

    public CalculatorController(ITariffCalculationService calculationService)
    {
        _calculationService = calculationService;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<TariffCalculationResult>> Calculate(
        [FromBody] TariffCalculationRequest request,
        CancellationToken cancellationToken)
    {
        var result = request.ReverseMode
            ? await _calculationService.ReverseCalculateAsync(request, cancellationToken)
            : await _calculationService.CalculateAsync(request, cancellationToken);

        return Ok(result);
    }
}
