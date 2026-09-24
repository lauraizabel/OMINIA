using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;

public sealed class DeleteSaleCommandValidator : AbstractValidator<DeleteSaleCommand>
{
    public DeleteSaleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithErrorCode(DomainErrorCodes.Sale.InvalidId);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0)
            .WithErrorCode(DomainErrorCodes.Sale.VersionConflict);
    }
}
