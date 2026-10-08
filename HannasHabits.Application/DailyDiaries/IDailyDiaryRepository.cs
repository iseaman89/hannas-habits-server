using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.DailyDiaries;

/// <summary>
/// Command side of the <see cref="DailyDiary"/> aggregate: returns tracked entities to change them. Lookups take the
/// owner's id, so a diary of another user is simply "not found". Persisting is done by <c>IUnitOfWork</c>.
/// </summary>
public interface IDailyDiaryRepository
{
    Task<DailyDiary?> GetByIdAsync(Guid userId, Guid diaryId, CancellationToken cancellationToken);

    void Add(DailyDiary dailyDiary);

    void Remove(DailyDiary dailyDiary);
}
