using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.DailyDiaries;

/// <summary>
/// Command side of the <see cref="DailyDiary"/> aggregate: returns tracked entities to change them. Lookups take the
/// owner's id, so a diary of another user is simply "not found". Persisting is done by <c>IUnitOfWork</c>.
/// </summary>
public interface IDailyDiaryRepository
{
    /// <summary>The user's entry for that day, or <c>null</c> if there is none.</summary>
    Task<DailyDiary?> GetByDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);

    void Add(DailyDiary dailyDiary);

    /// <summary>Removes a stored entry; for one that was only added (and not saved) it just forgets it again.</summary>
    void Remove(DailyDiary dailyDiary);
}
