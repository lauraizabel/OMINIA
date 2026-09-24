using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public sealed class CreateSaleHandler : IRequestHandler<CreateSaleCommand, SaleResult>
{
    private readonly ISaleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateSaleHandler(ISaleRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<SaleResult> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = Sale.Create(
            command.SaleNumber,
            command.SaleDate,
            command.Customer.ToDomain(),
            command.Branch.ToDomain(),
            command.Items.Select(item => item.ToDomain()),
            _timeProvider.GetUtcNow());

        if (await _repository.ExistsBySaleNumberAsync(sale.SaleNumber, cancellationToken))
        {
            throw new DomainConflictException(
                DomainErrorCodes.Sale.NumberAlreadyExists,
                $"A sale with number '{sale.SaleNumber}' already exists.");
        }

        await _repository.AddAsync(sale, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        return SaleResult.From(sale);
    }
}
