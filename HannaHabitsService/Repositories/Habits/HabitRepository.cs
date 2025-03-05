using HannaHabitsService.Data;
using HannaHabitsService.Models;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Repositories.Habits;

public class HabitRepository(HannaDbContext context) : GenericRepository<Habit>(context), IHabitRepository
{
    private readonly HannaDbContext _context = context;

    public async Task<IEnumerable<Habit>> GetHabitsByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.Habits.Where(h => h.UserId == userId)
            .Include(h => h.Completions)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<Habit?> GetHabitByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Habits.Where(h => h.Id == id)
            .Include(h => h.Completions)
            .FirstOrDefaultAsync(cancellationToken);
    }
}