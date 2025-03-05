using HannaHabitsService.Models;

namespace HannaHabitsService.Repositories.DailyDiaries;

public interface IDailyDiaryRepository : IGenericRepository<DailyDiary>
{
    Task<IEnumerable<DailyDiary>> GetDailyDiariesByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<DailyDiary?> GetDailyDiaryByIdAsync(int id, CancellationToken cancellationToken);
    Task<DailyDiary?> GetDailyDiaryByDayAsync(string userId, DateTime date, CancellationToken cancellationToken);

    Task<IEnumerable<CalendarItem>> GetDailyDiaryIdsForCalendarAsync(string userId, CancellationToken cancellationToken);
}