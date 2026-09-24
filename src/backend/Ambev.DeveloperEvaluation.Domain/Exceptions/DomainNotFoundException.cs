namespace Ambev.DeveloperEvaluation.Domain.Exceptions;

public sealed class DomainNotFoundException : global::DomainException
{
    public DomainNotFoundException(string code, string message)
        : base(code, message)
    {
    }
}
