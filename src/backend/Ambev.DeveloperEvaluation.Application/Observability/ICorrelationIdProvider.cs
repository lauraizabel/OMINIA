namespace Ambev.DeveloperEvaluation.Application.Observability;

public interface ICorrelationIdProvider
{
    string CorrelationId { get; }
}
