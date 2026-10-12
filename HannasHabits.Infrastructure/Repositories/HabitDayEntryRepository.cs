using HannasHabits.Application.HabitDays;
using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Repositories;

public class HabitDayEntryRepository : IHabitDayEntryRepository
{
    private readonly ApplicationDbContext _context;

    public HabitDayEntryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // The owner is the habit's owner. The condition sits in the query (not only in the caller's earlier habit lookup),
    // so that data isolation does not depend on a rule elsewhere.
    private IQueryable<HabitDayEntry> OwnedBy(Guid userId)
        => _context.HabitDayEntries
            .Include(e => e.Tags)
            .Where(e => _context.Habits.Any(h => h.Id == e.HabitId && h.UserId == userId));

    public Task<HabitDayEntry?> GetAsync(Guid userId, Guid habitId, DateOnly date, CancellationToken cancellationToken)
        => OwnedBy(userId)
            .FirstOrDefaultAsync(e => e.HabitId == habitId && e.Date == date, cancellationToken);

    public Task<List<HabitDayEntry>> GetWithTagAsync(Guid userId, Guid tagId, CancellationToken cancellationToken)
        => OwnedBy(userId)
            .Where(e => e.Tags.Any(t => t.TagId == tagId))
            .OrderBy(e => e.Date).ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

    public void Add(HabitDayEntry entry) => _context.HabitDayEntries.Add(entry);

    // For an entry that was only added (its insert failed), EF Core just stops tracking it.
    public void Remove(HabitDayEntry entry) => _context.HabitDayEntries.Remove(entry);
}
