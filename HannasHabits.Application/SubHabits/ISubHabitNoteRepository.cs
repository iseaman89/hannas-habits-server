using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits;

/// <summary>
/// Command side of the <see cref="SubHabitNote"/> aggregate: returns tracked entities to change them. A note belongs to
/// a part, the part to a habit and the habit to a user: every lookup takes the owner's id, so the note of another user's
/// part is simply "not found". Persisting is done by <c>IUnitOfWork</c>.
/// </summary>
public interface ISubHabitNoteRepository
{
    /// <summary>The note of that part and day, or <c>null</c> if the day has none.</summary>
    Task<SubHabitNote?> GetAsync(Guid userId, Guid subHabitId, DateOnly date, CancellationToken cancellationToken);

    void Add(SubHabitNote note);

    /// <summary>Removes a stored note; for one that was only added (and not saved) it just forgets it again.</summary>
    void Remove(SubHabitNote note);
}
