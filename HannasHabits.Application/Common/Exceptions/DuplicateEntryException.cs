namespace HannasHabits.Application.Common.Exceptions;

/// <summary>
/// A unique index rejected the save because an equal entry already exists, typically because a concurrent request
/// created it first. A <see cref="ConflictException"/> (409) unless a handler knows a better way to react.
/// </summary>
public class DuplicateEntryException : ConflictException
{
    public DuplicateEntryException(Exception innerException)
        : base("An equal entry already exists.", innerException)
    {
    }
}
