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
    public Task<Habit?> GetByIdWithRecordsAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => _context.Habits
            .Include(h => h.Records)
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);

    public void Add(Habit habit) => _context.Habits.Add(habit);

    public void Remove(Habit habit) => _context.Habits.Remove(habit);
}
