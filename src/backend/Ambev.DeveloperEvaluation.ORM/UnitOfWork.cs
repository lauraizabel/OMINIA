using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.ORM;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DefaultContext _context;

    public UnitOfWork(DefaultContext context)
    {
        _context = context;
    }

    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
