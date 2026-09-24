using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public sealed class GetSaleQueryValidator : AbstractValidator<GetSaleQuery>
{
    public GetSaleQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty().WithErrorCode(DomainErrorCodes.Sale.InvalidId);
    }
}
