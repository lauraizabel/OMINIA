using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

internal sealed class ExternalIdentityInputValidator : AbstractValidator<ExternalIdentityInput>
{
    public ExternalIdentityInputValidator()
    {
        RuleFor(value => value.ExternalId)
            .NotEmpty().WithErrorCode(DomainErrorCodes.ExternalIdentity.InvalidId)
            .MaximumLength(ExternalIdentity.ExternalIdMaximumLength)
            .WithErrorCode(DomainErrorCodes.ExternalIdentity.InvalidId);
        RuleFor(value => value.Name)
            .NotEmpty().WithErrorCode(DomainErrorCodes.ExternalIdentity.InvalidName)
            .MaximumLength(ExternalIdentity.NameMaximumLength)
            .WithErrorCode(DomainErrorCodes.ExternalIdentity.InvalidName);
    }
}

internal sealed class SaleItemInputValidator : AbstractValidator<SaleItemInput>
{
    public SaleItemInputValidator()
    {
        RuleFor(value => value.Product).NotNull()
            .WithErrorCode(DomainErrorCodes.SaleItem.ProductRequired)
            .SetValidator(new ExternalIdentityInputValidator()!);
        RuleFor(value => value.Quantity)
            .InclusiveBetween(SaleDiscountPolicy.MinimumQuantity, SaleDiscountPolicy.MaximumQuantity)
            .WithErrorCode(DomainErrorCodes.SaleItem.QuantityOutOfRange);
        RuleFor(value => value.UnitPrice)
            .GreaterThan(0).LessThanOrEqualTo(SaleDiscountPolicy.MaximumUnitPrice)
            .WithErrorCode(DomainErrorCodes.SaleItem.UnitPriceOutOfRange)
            .Must(value => decimal.Round(value, 2) == value)
            .WithErrorCode(DomainErrorCodes.SaleItem.UnitPriceScaleExceeded)
            .WithMessage("Unit price must contain at most two decimal places.");
    }
}
