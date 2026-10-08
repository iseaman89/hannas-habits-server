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

    public Task<Habit?> GetByIdWithRecordsAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => _context.Habits
            .Include(h => h.Records)
            .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);

    public void Add(Habit habit) => _context.Habits.Add(habit);

    public void Remove(Habit habit) => _context.Habits.Remove(habit);
}
