using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public sealed class CancelSaleItemCommandValidator : AbstractValidator<CancelSaleItemCommand>
{
    public CancelSaleItemCommandValidator()
    {
        RuleFor(command => command.SaleId).NotEmpty().WithErrorCode(DomainErrorCodes.Sale.InvalidId);
        RuleFor(command => command.ItemId).NotEmpty().WithErrorCode(DomainErrorCodes.SaleItem.InvalidId);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0)
            .WithErrorCode(DomainErrorCodes.Sale.VersionConflict);
    }
}
