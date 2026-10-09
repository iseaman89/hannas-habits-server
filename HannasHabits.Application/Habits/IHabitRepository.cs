using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Habits;

/// <summary>
/// Command side of the <see cref="Habit"/> aggregate: returns tracked entities to change them. Every lookup takes the
/// owner's id, so a habit of another user is simply "not found". Persisting is done by <c>IUnitOfWork</c>.
/// </summary>
public interface IHabitRepository
{
    /// <summary>The user's habit without its records, or <c>null</c>.</summary>
    Task<Habit?> GetByIdAsync(Guid userId, Guid habitId, CancellationToken cancellationToken);

    /// <summary>The user's habit including all its records (needed to mark or unmark days), or <c>null</c>.</summary>
    Task<Habit?> GetByIdWithRecordsAsync(Guid userId, Guid habitId, CancellationToken cancellationToken);

    void Add(Habit habit);

    void Remove(Habit habit);
}
