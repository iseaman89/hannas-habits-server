namespace HannasHabits.Application.Common.Exceptions;

/// <summary>
/// The request is valid, but conflicts with the current state (e.g. the resource already exists).
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
