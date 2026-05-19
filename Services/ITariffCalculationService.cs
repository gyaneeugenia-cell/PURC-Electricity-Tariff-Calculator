using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public interface ITariffCalculationService
{
    Task<TariffCalculationResult> CalculateAsync(
        TariffCalculationRequest request,
        CancellationToken cancellationToken = default);

    Task<TariffCalculationResult> ReverseCalculateAsync(
        TariffCalculationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoricTrendRow>> BuildHistoricTrendAsync(
        HistoricTrendRequest request,
        CancellationToken cancellationToken = default);
}
