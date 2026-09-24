using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;

public sealed class DeleteSaleHandler : IRequestHandler<DeleteSaleCommand, Unit>
{
    private readonly ISaleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public DeleteSaleHandler(ISaleRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Unit> Handle(DeleteSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdIncludingDeletedAsync(command.Id, cancellationToken)
            ?? throw new DomainNotFoundException(
                DomainErrorCodes.Sale.NotFound,
                $"Sale '{command.Id}' was not found.");

        if (sale.IsDeleted)
        {
            EnsureValidReplay(sale.Version, command.ExpectedVersion);
            return Unit.Value;
        }

        SaleVersionGuard.EnsureMatches(sale, command.ExpectedVersion);
        sale.Delete(_timeProvider.GetUtcNow());
        await _unitOfWork.CommitAsync(cancellationToken);
        return Unit.Value;
    }

    private static void EnsureValidReplay(long deletedVersion, long expectedVersion)
    {
        if (expectedVersion == deletedVersion - 1)
            return;

        throw new DomainConcurrencyException(
            DomainErrorCodes.Sale.VersionConflict,
            "The sale changed after it was retrieved.");
    }
}
