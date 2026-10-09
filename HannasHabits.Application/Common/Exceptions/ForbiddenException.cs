namespace HannasHabits.Application.Common.Exceptions;

/// <summary>
/// The caller is authenticated, but not allowed to do this. Not used for other users' data:
/// those resources are reported as <see cref="NotFoundException"/> so their existence is not leaked.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
