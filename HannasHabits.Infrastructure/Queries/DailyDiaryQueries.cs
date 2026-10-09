using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Queries;

public class DailyDiaryQueries : IDailyDiaryQueries
{
    private readonly ApplicationDbContext _context;

    public DailyDiaryQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DailyDiaryDto?> GetByDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        // The lists are converted columns (text[] / jsonb): SQL cannot look inside them, so the query loads the plain
        // columns and the DTO is built from them in memory.
        var entry = await _context.DailyDiaries
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.Date == date)
            .Select(d => new { d.Date, d.Mood, d.Body, d.Mind, d.Highlight, d.Grateful, d.Learned, d.Tasks })
            .FirstOrDefaultAsync(cancellationToken);

        return entry is null
            ? null
            : new DailyDiaryDto(
                entry.Date,
                entry.Mood,
                entry.Body?.Value,
                entry.Mind?.Value,
                entry.Highlight,
                entry.Grateful,
                entry.Learned,
                entry.Tasks.Select(task => new DiaryTaskDto(task.Title, task.Done)).ToList());
    }

    public Task<List<DailyDiaryDayDto>> GetDaysAsync(
        Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
        => _context.DailyDiaries
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .Where(d => from == null || d.Date >= from)
            .Where(d => to == null || d.Date <= to)
            .OrderBy(d => d.Date)
            .Select(d => new DailyDiaryDayDto(d.Date, d.Mood))
            .ToListAsync(cancellationToken);
}
