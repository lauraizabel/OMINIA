namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public interface ISaleReadService
{
    Task<PagedSalesResult> ListAsync(
        SaleListCriteria criteria,
        CancellationToken cancellationToken = default);
}
