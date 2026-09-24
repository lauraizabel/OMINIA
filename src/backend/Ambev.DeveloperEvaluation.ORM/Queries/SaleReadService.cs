using System.Data;
using System.Linq.Expressions;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Queries;

public sealed class SaleReadService : ISaleReadService
{
    private const string LikeEscapeCharacter = "\\";
    private static readonly Expression<Func<Sale, SaleSummaryResult>> SummaryProjection = sale =>
        new SaleSummaryResult(
            sale.Id,
            sale.SaleNumber,
            sale.SaleDate,
            new ExternalIdentityResult(sale.Customer.ExternalId, sale.Customer.Name),
            new ExternalIdentityResult(sale.Branch.ExternalId, sale.Branch.Name),
            sale.TotalAmount,
            sale.IsCancelled,
            sale.UpdatedAt,
            sale.Version);

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
            .Select(SummaryProjection)
            .ToArrayAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : ((totalItems - 1) / criteria.PageSize) + 1;

        return new PagedSalesResult(data, totalItems, criteria.Page, totalPages);
    }

    private static IQueryable<Sale> ApplyFilters(IQueryable<Sale> query, SaleListCriteria criteria)
    {
        query = ApplySaleNumberFilter(query, criteria.SaleNumber);

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

    private static IQueryable<Sale> ApplySaleNumberFilter(
        IQueryable<Sale> query,
        SaleNumberFilter? filter)
    {
        if (filter is null)
            return query;

        if (!filter.MatchStart && !filter.MatchEnd)
            return query.Where(sale => sale.SaleNumber == filter.Value);

        var escaped = EscapeLikePattern(filter.Value);
        var pattern = $"{(filter.MatchStart ? "%" : string.Empty)}{escaped}{(filter.MatchEnd ? "%" : string.Empty)}";
        return query.Where(sale => EF.Functions.Like(sale.SaleNumber, pattern, LikeEscapeCharacter));
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
        return term.Field switch
        {
            SaleOrderField.SaleDate => ApplyDirection(query, ordered, term.Direction, sale => sale.SaleDate),
            SaleOrderField.SaleNumber => ApplyDirection(query, ordered, term.Direction, sale => sale.SaleNumber),
            SaleOrderField.TotalAmount => ApplyDirection(query, ordered, term.Direction, sale => sale.TotalAmount),
            SaleOrderField.Id => ApplyDirection(query, ordered, term.Direction, sale => sale.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(term))
        };
    }

    private static IOrderedQueryable<Sale> ApplyDirection<TKey>(
        IQueryable<Sale> query,
        IOrderedQueryable<Sale>? ordered,
        SortDirection direction,
        Expression<Func<Sale, TKey>> keySelector)
    {
        if (ordered is null)
        {
            return direction == SortDirection.Ascending
                ? query.OrderBy(keySelector)
                : query.OrderByDescending(keySelector);
        }

        return direction == SortDirection.Ascending
            ? ordered.ThenBy(keySelector)
            : ordered.ThenByDescending(keySelector);
    }

    private static string EscapeLikePattern(string value) => value
        .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
        .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
        .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal);
}
