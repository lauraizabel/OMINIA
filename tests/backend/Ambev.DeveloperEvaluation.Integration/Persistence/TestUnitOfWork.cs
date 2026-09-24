using Ambev.DeveloperEvaluation.Application.Observability;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.ORM;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ambev.DeveloperEvaluation.Integration.Persistence;

internal static class TestUnitOfWork
{
    public static UnitOfWork Create(
        DefaultContext context,
        ISaleDomainEventPublisher? eventPublisher = null,
        string correlationId = "integration-test")
    {
        return new UnitOfWork(
            context,
            eventPublisher ?? NullPublisher.Instance,
            new FixedCorrelationIdProvider(correlationId),
            NullLogger<UnitOfWork>.Instance);
    }

    private sealed class NullPublisher : ISaleDomainEventPublisher
    {
        public static NullPublisher Instance { get; } = new();

        public Task PublishAsync(
            SaleDomainEvent domainEvent,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record FixedCorrelationIdProvider(string CorrelationId) : ICorrelationIdProvider;
}
