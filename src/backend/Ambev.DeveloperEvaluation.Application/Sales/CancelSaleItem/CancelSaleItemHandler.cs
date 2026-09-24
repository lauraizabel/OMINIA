using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public sealed class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, SaleResult>
{
    private readonly ISaleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CancelSaleItemHandler(ISaleRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<SaleResult> Handle(CancelSaleItemCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(command.SaleId, cancellationToken)
            ?? throw new DomainNotFoundException(
                DomainErrorCodes.Sale.NotFound,
                $"Sale '{command.SaleId}' was not found.");

        SaleVersionGuard.EnsureMatches(sale, command.ExpectedVersion);
        if (sale.CancelItem(command.ItemId, _timeProvider.GetUtcNow()))
            await _unitOfWork.CommitAsync(cancellationToken);

        return SaleResult.From(sale);
    }
}
