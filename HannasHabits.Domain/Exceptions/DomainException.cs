namespace HannasHabits.Domain.Exceptions;

/// <summary>
/// Thrown by entities when a business rule (invariant) is violated.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
