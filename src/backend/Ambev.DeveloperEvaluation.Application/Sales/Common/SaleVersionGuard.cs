using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;

namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

public static class SaleVersionGuard
{
    public static void EnsureMatches(Sale sale, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(sale);
        if (sale.Version == expectedVersion)
            return;

        throw new DomainConcurrencyException(
            DomainErrorCodes.Sale.VersionConflict,
            "The sale changed after it was retrieved.");
    }
}
