public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message) : this("DomainError", message)
    {
    }

    public DomainException(string message, Exception innerException)
        : this("DomainError", message, innerException)
    {
    }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
