using HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;

namespace HannasHabits.Application.DailyDiaries;

/// <summary>
/// Read side for daily diaries: projects straight to DTOs, nothing is tracked. Every method takes the owner's id,
/// data of other users is never returned.
/// </summary>
public interface IDailyDiaryQueries
{
    Task<List<DailyDiaryListItemDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary><c>null</c> if the diary does not exist or belongs to another user.</summary>
    Task<DailyDiaryDetailsDto?> GetByIdAsync(Guid userId, Guid diaryId, CancellationToken cancellationToken);
}
