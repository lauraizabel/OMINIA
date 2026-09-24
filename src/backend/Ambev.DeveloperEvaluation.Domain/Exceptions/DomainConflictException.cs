namespace Ambev.DeveloperEvaluation.Domain.Exceptions;

public sealed class DomainConflictException : global::DomainException
{
    public DomainConflictException(string code, string message)
        : base(code, message)
    {
    }
}
