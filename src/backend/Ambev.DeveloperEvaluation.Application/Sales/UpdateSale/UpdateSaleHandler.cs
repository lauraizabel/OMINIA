using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public sealed class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, SaleResult>
{
    private readonly ISaleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public UpdateSaleHandler(ISaleRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<SaleResult> Handle(UpdateSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new DomainNotFoundException(
                DomainErrorCodes.Sale.NotFound,
                $"Sale '{command.Id}' was not found.");

        if (sale.Version != command.ExpectedVersion)
        {
            throw new DomainConcurrencyException(
                DomainErrorCodes.Sale.VersionConflict,
                "The sale changed after it was retrieved.");
        }

        var changed = sale.Update(
            command.SaleDate,
            command.Customer.ToDomain(),
            command.Branch.ToDomain(),
            command.Items.Select(item => item.ToDomain()),
            _timeProvider.GetUtcNow());

        if (changed)
            await _unitOfWork.CommitAsync(cancellationToken);

        return SaleResult.From(sale);
    }
}
