namespace Ambev.DeveloperEvaluation.Domain.Exceptions;

public sealed class DomainConcurrencyException : global::DomainException
{
    public DomainConcurrencyException(string code, string message)
        : base(code, message)
    {
    }
}
