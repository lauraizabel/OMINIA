using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public sealed class CancelSaleCommandValidator : AbstractValidator<CancelSaleCommand>
{
    public CancelSaleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithErrorCode(DomainErrorCodes.Sale.InvalidId);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0)
            .WithErrorCode(DomainErrorCodes.Sale.VersionConflict);
    }
}
