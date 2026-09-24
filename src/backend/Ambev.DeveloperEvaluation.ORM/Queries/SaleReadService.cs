using System.Data;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Queries;

public sealed class SaleReadService : ISaleReadService
{
    private const string LikeEscapeCharacter = "\\";
    private readonly DefaultContext _context;

    public SaleReadService(DefaultContext context) => _context = context;

    public async Task<PagedSalesResult> ListAsync(
        SaleListCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var query = ApplyFilters(_context.Sales.AsNoTracking(), criteria);
        // Count and page must observe the same snapshot so response metadata cannot
        // disagree with the returned rows when concurrent writes are committed.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        var totalItems = await query.LongCountAsync(cancellationToken);
        var offset = checked((criteria.Page - 1) * criteria.PageSize);
        var data = await ApplyOrder(query, criteria.Order)
            .Skip(offset)
            .Take(criteria.PageSize)
            .Select(sale => new SaleSummaryResult(
                sale.Id,
                sale.SaleNumber,
                sale.SaleDate,
                new ExternalIdentityResult(sale.Customer.ExternalId, sale.Customer.Name),
                new ExternalIdentityResult(sale.Branch.ExternalId, sale.Branch.Name),
                sale.TotalAmount,
                sale.IsCancelled,
                sale.UpdatedAt,
                sale.Version))
            .ToArrayAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : ((totalItems - 1) / criteria.PageSize) + 1;

        return new PagedSalesResult(data, totalItems, criteria.Page, totalPages);
    }

    private static IQueryable<Sale> ApplyFilters(IQueryable<Sale> query, SaleListCriteria criteria)
    {
        if (criteria.SaleNumber is not null)
        {
            var filter = criteria.SaleNumber;
            if (!filter.MatchStart && !filter.MatchEnd)
            {
                query = query.Where(sale => sale.SaleNumber == filter.Value);
            }
            else
            {
                var escaped = EscapeLikePattern(filter.Value);
                var pattern = $"{(filter.MatchStart ? "%" : string.Empty)}{escaped}{(filter.MatchEnd ? "%" : string.Empty)}";
                query = query.Where(sale => EF.Functions.Like(sale.SaleNumber, pattern, LikeEscapeCharacter));
            }
        }

        if (criteria.CustomerExternalIds.Count > 0)
            query = query.Where(sale => criteria.CustomerExternalIds.Contains(sale.Customer.ExternalId));

        if (criteria.BranchExternalIds.Count > 0)
            query = query.Where(sale => criteria.BranchExternalIds.Contains(sale.Branch.ExternalId));

        if (criteria.CancellationStates.Count > 0)
            query = query.Where(sale => criteria.CancellationStates.Contains(sale.IsCancelled));

        if (criteria.MinimumSaleDate.HasValue)
            query = query.Where(sale => sale.SaleDate >= criteria.MinimumSaleDate.Value);

        if (criteria.MaximumSaleDate.HasValue)
            query = query.Where(sale => sale.SaleDate <= criteria.MaximumSaleDate.Value);

        if (criteria.MinimumTotalAmount.HasValue)
            query = query.Where(sale => sale.TotalAmount >= criteria.MinimumTotalAmount.Value);

        if (criteria.MaximumTotalAmount.HasValue)
            query = query.Where(sale => sale.TotalAmount <= criteria.MaximumTotalAmount.Value);

        return query;
    }

    private static IOrderedQueryable<Sale> ApplyOrder(
        IQueryable<Sale> query,
        IReadOnlyCollection<SaleOrder> terms)
    {
        IOrderedQueryable<Sale>? ordered = null;
        foreach (var term in terms)
            ordered = ApplyOrderTerm(query, ordered, term);

        return ordered ?? query.OrderByDescending(sale => sale.SaleDate).ThenBy(sale => sale.Id);
    }

    private static IOrderedQueryable<Sale> ApplyOrderTerm(
        IQueryable<Sale> query,
        IOrderedQueryable<Sale>? ordered,
        SaleOrder term)
    {
        return (term.Field, term.Direction, ordered) switch
        {
            (SaleOrderField.SaleDate, SortDirection.Ascending, null) => query.OrderBy(sale => sale.SaleDate),
            (SaleOrderField.SaleDate, SortDirection.Descending, null) => query.OrderByDescending(sale => sale.SaleDate),
            (SaleOrderField.SaleNumber, SortDirection.Ascending, null) => query.OrderBy(sale => sale.SaleNumber),
            (SaleOrderField.SaleNumber, SortDirection.Descending, null) => query.OrderByDescending(sale => sale.SaleNumber),
            (SaleOrderField.TotalAmount, SortDirection.Ascending, null) => query.OrderBy(sale => sale.TotalAmount),
            (SaleOrderField.TotalAmount, SortDirection.Descending, null) => query.OrderByDescending(sale => sale.TotalAmount),
            (SaleOrderField.Id, SortDirection.Ascending, null) => query.OrderBy(sale => sale.Id),
            (SaleOrderField.Id, SortDirection.Descending, null) => query.OrderByDescending(sale => sale.Id),
            (SaleOrderField.SaleDate, SortDirection.Ascending, _) => ordered.ThenBy(sale => sale.SaleDate),
            (SaleOrderField.SaleDate, SortDirection.Descending, _) => ordered.ThenByDescending(sale => sale.SaleDate),
            (SaleOrderField.SaleNumber, SortDirection.Ascending, _) => ordered.ThenBy(sale => sale.SaleNumber),
            (SaleOrderField.SaleNumber, SortDirection.Descending, _) => ordered.ThenByDescending(sale => sale.SaleNumber),
            (SaleOrderField.TotalAmount, SortDirection.Ascending, _) => ordered.ThenBy(sale => sale.TotalAmount),
            (SaleOrderField.TotalAmount, SortDirection.Descending, _) => ordered.ThenByDescending(sale => sale.TotalAmount),
            (SaleOrderField.Id, SortDirection.Ascending, _) => ordered.ThenBy(sale => sale.Id),
            (SaleOrderField.Id, SortDirection.Descending, _) => ordered.ThenByDescending(sale => sale.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(term))
        };
    }

    private static string EscapeLikePattern(string value) => value
        .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
        .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
        .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal);
}
