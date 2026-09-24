using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Domain.Repositories;

public interface ISaleRepository
{
    Task AddAsync(Sale sale, CancellationToken cancellationToken = default);

    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Sale?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Sale?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySaleNumberAsync(string saleNumber, CancellationToken cancellationToken = default);
}
