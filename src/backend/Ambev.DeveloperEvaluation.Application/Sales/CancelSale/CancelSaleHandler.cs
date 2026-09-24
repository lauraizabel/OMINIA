using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public sealed class CancelSaleHandler : IRequestHandler<CancelSaleCommand, SaleResult>
{
    private readonly ISaleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CancelSaleHandler(ISaleRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<SaleResult> Handle(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new DomainNotFoundException(
                DomainErrorCodes.Sale.NotFound,
                $"Sale '{command.Id}' was not found.");

        SaleVersionGuard.EnsureMatches(sale, command.ExpectedVersion);
        if (sale.Cancel(_timeProvider.GetUtcNow()))
            await _unitOfWork.CommitAsync(cancellationToken);

        return SaleResult.From(sale);
    }
}
