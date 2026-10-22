namespace EcommerceApi.Domain.Common;

/// <summary>
/// Raised when a domain invariant is violated. These are programming or data
/// integrity errors, not expected user input failures: request validation happens
/// in the API layer before the domain is reached.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
