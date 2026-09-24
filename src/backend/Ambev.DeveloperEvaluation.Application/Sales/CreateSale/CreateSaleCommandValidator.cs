using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(command => command.SaleNumber)
            .NotEmpty().WithErrorCode(DomainErrorCodes.Sale.NumberRequired)
            .MaximumLength(Sale.SaleNumberMaximumLength).WithErrorCode(DomainErrorCodes.Sale.NumberTooLong);
        RuleFor(command => command.SaleDate).NotEmpty().WithErrorCode(DomainErrorCodes.Sale.DateRequired);
        RuleFor(command => command.Customer).NotNull()
            .WithErrorCode(DomainErrorCodes.Sale.CustomerRequired)
            .SetValidator(new Common.ExternalIdentityInputValidator()!);
        RuleFor(command => command.Branch).NotNull()
            .WithErrorCode(DomainErrorCodes.Sale.BranchRequired)
            .SetValidator(new Common.ExternalIdentityInputValidator()!);
        RuleFor(command => command.Items).NotNull().NotEmpty()
            .WithErrorCode(DomainErrorCodes.Sale.ItemsRequired)
            .Must(items => items is null || items.Count <= Sale.MaximumItems)
            .WithErrorCode(DomainErrorCodes.Sale.TooManyItems);
        RuleForEach(command => command.Items).NotNull()
            .WithErrorCode(DomainErrorCodes.SaleItem.NullItem)
            .SetValidator(new Common.SaleItemInputValidator()!);
    }
}
