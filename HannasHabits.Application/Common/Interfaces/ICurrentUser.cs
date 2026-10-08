namespace HannasHabits.Application.Common.Interfaces;

public interface ICurrentUser
{
    /// <summary>Id of the authenticated user. Throws <see cref="UnauthorizedAccessException"/> if there is none.</summary>
    Guid UserId { get; }
}
