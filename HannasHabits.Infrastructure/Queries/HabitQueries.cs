using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.Habits;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Queries;

public class HabitQueries : IHabitQueries
{
    private readonly ApplicationDbContext _context;

    public HabitQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<HabitListItemDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => _context.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .Select(h => new HabitListItemDto(h.Id, h.Title, h.Description))
            .ToListAsync(cancellationToken);

    public Task<HabitDetailsDto?> GetByIdAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => _context.Habits
            .AsNoTracking()
            .Where(h => h.Id == habitId && h.UserId == userId)
            .Select(h => new HabitDetailsDto(h.Id, h.Title, h.Description, h.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<List<HabitRecordDto>?> GetRecordsAsync(
        Guid userId, Guid habitId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        // One round trip: the user filter sits on the habit, so a foreign or unknown habit yields no row (null)
        // while an existing habit without records yields an empty list.
        var habit = await _context.Habits
            .AsNoTracking()
            .Where(h => h.Id == habitId && h.UserId == userId)
            .Select(h => new
            {
                Records = h.Records
                    .Where(r => from == null || r.Date >= from)
                    .Where(r => to == null || r.Date <= to)
                    .OrderBy(r => r.Date)
                    .Select(r => new HabitRecordDto(r.Id, r.HabitId, r.Date))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return habit?.Records;
    }
}
