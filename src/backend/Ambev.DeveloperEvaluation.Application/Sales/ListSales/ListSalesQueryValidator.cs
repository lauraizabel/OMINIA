using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public sealed class ListSalesQueryValidator : AbstractValidator<ListSalesQuery>
{
    public ListSalesQueryValidator()
    {
        RuleFor(query => query.Criteria).NotNull();
        When(query => query.Criteria is not null, () =>
        {
            RuleFor(query => query.Criteria.Page)
                .GreaterThan(0)
                .WithErrorCode(SaleListErrorCodes.InvalidPage);
            RuleFor(query => query.Criteria.PageSize)
                .InclusiveBetween(1, 100)
                .WithErrorCode(SaleListErrorCodes.InvalidPageSize);
            RuleFor(query => query.Criteria)
                .Must(criteria => (long)(criteria.Page - 1) * criteria.PageSize <= int.MaxValue)
                .WithName("Page")
                .WithErrorCode(SaleListErrorCodes.InvalidPage)
                .WithMessage("The requested page is too large.");
            RuleFor(query => query.Criteria.Order)
                .NotEmpty()
                .Must(order => order.Select(term => term.Field).Distinct().Count() == order.Count)
                .Must(order => order.LastOrDefault()?.Field == SaleOrderField.Id)
                .WithErrorCode(SaleListErrorCodes.InvalidOrder);
            RuleFor(query => query.Criteria.CustomerExternalIds)
                .Must(values => values.Count <= 20)
                .WithErrorCode(SaleListErrorCodes.TooManyValues);
            RuleFor(query => query.Criteria.BranchExternalIds)
                .Must(values => values.Count <= 20)
                .WithErrorCode(SaleListErrorCodes.TooManyValues);
            RuleFor(query => query.Criteria.CancellationStates)
                .Must(values => values.Count <= 20)
                .WithErrorCode(SaleListErrorCodes.TooManyValues);
            RuleFor(query => query.Criteria)
                .Must(criteria =>
                    !criteria.MinimumSaleDate.HasValue ||
                    !criteria.MaximumSaleDate.HasValue ||
                    criteria.MinimumSaleDate <= criteria.MaximumSaleDate)
                .WithName("MinimumSaleDate")
                .WithErrorCode(SaleListErrorCodes.InvalidRange);
            RuleFor(query => query.Criteria)
                .Must(criteria =>
                    !criteria.MinimumTotalAmount.HasValue ||
                    !criteria.MaximumTotalAmount.HasValue ||
                    criteria.MinimumTotalAmount <= criteria.MaximumTotalAmount)
                .WithName("MinimumTotalAmount")
                .WithErrorCode(SaleListErrorCodes.InvalidRange);
        });
    }
}
