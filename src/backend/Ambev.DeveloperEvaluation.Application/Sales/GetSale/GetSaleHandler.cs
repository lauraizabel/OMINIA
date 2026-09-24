using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public sealed class GetSaleHandler : IRequestHandler<GetSaleQuery, SaleResult>
{
    private readonly ISaleRepository _repository;

    public GetSaleHandler(ISaleRepository repository) => _repository = repository;

    public async Task<SaleResult> Handle(GetSaleQuery query, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdReadOnlyAsync(query.Id, cancellationToken)
            ?? throw new DomainNotFoundException(
                DomainErrorCodes.Sale.NotFound,
                $"Sale '{query.Id}' was not found.");

        return SaleResult.From(sale);
    }
}
