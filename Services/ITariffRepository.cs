using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public interface ITariffRepository
{
    Task<IReadOnlyList<TariffPeriodCatalogItem>> GetPeriodCatalogAsync(CancellationToken cancellationToken = default);

    Task<int> ResolveYearIdByPeriodAsync(
        string period,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TariffCategoryComponent>> GetCategoryComponentTypesAsync(
        int yearId,
        string period,
        CancellationToken cancellationToken = default);

    Task<TariffBundle> GetTariffBundleAsync(
        int yearId,
        string period,
        int categoryId,
        CancellationToken cancellationToken = default);

    Task<TariffAdminUpdateResult> UpsertTariffEntryAsync(
        TariffAdminUpdateInput input,
        CancellationToken cancellationToken = default);
}
