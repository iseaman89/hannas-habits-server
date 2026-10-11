using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Repositories;

public class HabitRepository : IHabitRepository
{
    private readonly ApplicationDbContext _context;

    public HabitRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Habit?> GetByIdAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => _context.Habits
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);

    // Decision (B4): the aggregate is loaded with ALL its records, not only the day in question. A habit has at most
    // one record per day (~365 a year), so this stays cheap for years, and a complete aggregate lets the Domain
    // enforce rules across records (e.g. streaks later). Growth is handled on the read side, which queries records by
    // date range. If it ever hurts: Include(h => h.Records.Where(r => r.Date == date)) loads only the relevant day.
    // The sub-habits come in the order they were added (the Domain's "due sub-habits" and the answer's ids follow it)
    // with all their ticks, for the same reason as the records: a few per day, and a complete aggregate lets the Domain
    // keep the habit's day and its parts in step.
    public Task<Habit?> GetByIdWithRecordsAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => _context.Habits
            .Include(h => h.Records)
            .Include(h => h.SubHabits.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id))
                .ThenInclude(s => s.Records)
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);

    public Task<Habit?> GetByIdWithSubHabitsAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => _context.Habits
            .Include(h => h.SubHabits.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id))
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);

    public void Add(Habit habit) => _context.Habits.Add(habit);

    public void Remove(Habit habit) => _context.Habits.Remove(habit);
}
