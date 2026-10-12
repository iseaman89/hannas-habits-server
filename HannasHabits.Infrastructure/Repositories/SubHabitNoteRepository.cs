using HannasHabits.Application.SubHabits;
using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Repositories;

public class SubHabitNoteRepository : ISubHabitNoteRepository
{
    private readonly ApplicationDbContext _context;

    public SubHabitNoteRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // The owner is the owner of the part's habit. The condition sits in the query (not only in the caller's earlier habit
    // lookup), so that data isolation does not depend on a rule elsewhere.
    public Task<SubHabitNote?> GetAsync(
        Guid userId, Guid subHabitId, DateOnly date, CancellationToken cancellationToken)
        => _context.SubHabitNotes
            .Where(n => n.SubHabitId == subHabitId && n.Date == date)
            .Where(n => _context.SubHabits.Any(s => s.Id == n.SubHabitId
                                                    && _context.Habits.Any(h => h.Id == s.HabitId && h.UserId == userId)))
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(SubHabitNote note) => _context.SubHabitNotes.Add(note);

    // For a note that was only added (its insert failed), EF Core just stops tracking it.
    public void Remove(SubHabitNote note) => _context.SubHabitNotes.Remove(note);
}
