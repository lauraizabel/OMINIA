using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly DefaultContext _context;

    public SaleRepository(DefaultContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);
        await _context.Sales.AddAsync(sale, cancellationToken);
    }

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return QueryAggregate(_context.Sales)
            .SingleOrDefaultAsync(sale => sale.Id == id, cancellationToken);
    }

    public Task<Sale?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return QueryAggregate(_context.Sales.AsNoTracking())
            .SingleOrDefaultAsync(sale => sale.Id == id, cancellationToken);
    }

    public Task<Sale?> GetByIdIncludingDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return QueryAggregate(_context.Sales.IgnoreQueryFilters())
            .SingleOrDefaultAsync(sale => sale.Id == id, cancellationToken);
    }

    public Task<bool> ExistsBySaleNumberAsync(
        string saleNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saleNumber);
        return _context.Sales
            .IgnoreQueryFilters()
            .AnyAsync(sale => sale.SaleNumber == saleNumber, cancellationToken);
    }

    private static IQueryable<Sale> QueryAggregate(IQueryable<Sale> sales)
    {
        return sales.Include(sale => sale.Items);
    }
}
