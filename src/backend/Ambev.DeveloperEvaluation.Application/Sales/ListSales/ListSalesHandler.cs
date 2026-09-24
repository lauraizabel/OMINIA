using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public sealed class ListSalesHandler : IRequestHandler<ListSalesQuery, PagedSalesResult>
{
    private readonly ISaleReadService _readService;

    public ListSalesHandler(ISaleReadService readService) => _readService = readService;

    public Task<PagedSalesResult> Handle(ListSalesQuery query, CancellationToken cancellationToken) =>
        _readService.ListAsync(query.Criteria, cancellationToken);
}
