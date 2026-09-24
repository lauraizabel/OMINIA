namespace Ambev.DeveloperEvaluation.Domain.Exceptions;

public sealed class DomainValidationException : global::DomainException
{
    public DomainValidationException(string code, string message)
        : base(code, message)
    {
    }
}
