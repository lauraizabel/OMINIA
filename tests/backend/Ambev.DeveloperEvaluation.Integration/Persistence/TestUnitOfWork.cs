using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.ORM;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

internal static class TestUnitOfWork
{
    public static UnitOfWork Create(
        DefaultContext context,
        string correlationId = "integration-test",
        TimeProvider? timeProvider = null)
    {
        return new UnitOfWork(
            context,
            new FixedCorrelationIdProvider(correlationId),
            timeProvider ?? TimeProvider.System);
    }

    private sealed record FixedCorrelationIdProvider(string CorrelationId) : ICorrelationIdProvider;
}
