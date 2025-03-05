using HannaHabitsService.Data;
using HannaHabitsService.Models;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Repositories.DailyDiaries;

public class DailyDiaryRepository(HannaDbContext context) : GenericRepository<DailyDiary>(context), IDailyDiaryRepository
{
    private readonly HannaDbContext _context = context;

    public async Task<IEnumerable<DailyDiary>> GetDailyDiariesByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.DailyDiaries.Where(d => d.UserId == userId)
            .Include(d => d.DailyTasks)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<DailyDiary?> GetDailyDiaryByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.DailyDiaries.Where(d => d.Id == id)
            .Include(d => d.DailyTasks)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DailyDiary?> GetDailyDiaryByDayAsync(string userId, DateTime date, CancellationToken cancellationToken)
    {
        return await _context.DailyDiaries.Where(d => d.UserId == userId && d.Date.Date == date.Date)
            .Include(d => d.DailyTasks)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<CalendarItem>> GetDailyDiaryIdsForCalendarAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.DailyDiaries.Where(d => d.UserId == userId).Select(d => new CalendarItem() {DailyDiaryId = d.Id, Date = d.Date}).ToListAsync(cancellationToken);
    }
}