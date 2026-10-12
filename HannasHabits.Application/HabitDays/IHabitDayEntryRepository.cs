using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.HabitDays;

/// <summary>
/// Command side of the <see cref="HabitDayEntry"/> aggregate: returns tracked entities (with their tags) to change them.
/// An entry belongs to a habit, and the habit to a user: every lookup takes the owner's id, so the entry of another
/// user's habit is simply "not found". Persisting is done by <c>IUnitOfWork</c>.
/// </summary>
public interface IHabitDayEntryRepository
{
    /// <summary>The entry of that habit and day, or <c>null</c> if the day has no tags.</summary>
    Task<HabitDayEntry?> GetAsync(Guid userId, Guid habitId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>Every entry (of any of the user's habits) that carries the tag, with ALL of its tags.</summary>
    Task<List<HabitDayEntry>> GetWithTagAsync(Guid userId, Guid tagId, CancellationToken cancellationToken);

    void Add(HabitDayEntry entry);

    /// <summary>Removes a stored entry; for one that was only added (and not saved) it just forgets it again.</summary>
    void Remove(HabitDayEntry entry);
}
