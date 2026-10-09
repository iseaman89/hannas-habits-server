using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;

namespace HannasHabits.Application.DailyDiaries;

/// <summary>
/// Read side for daily diaries: projects straight to DTOs, nothing is tracked. Every method takes the owner's id,
/// data of other users is never returned.
/// </summary>
public interface IDailyDiaryQueries
{
    /// <summary><c>null</c> if the user has no entry for that day.</summary>
    Task<DailyDiaryDto?> GetByDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>The days with an entry and their mood, oldest first. <paramref name="from"/>/<paramref name="to"/> are inclusive and optional.</summary>
    Task<List<DailyDiaryDayDto>> GetDaysAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}
