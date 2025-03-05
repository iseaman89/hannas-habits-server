using HannaHabitsService.Models;

namespace HannaHabitsService.Repositories.Habits;

public interface IHabitRepository : IGenericRepository<Habit>
{
    Task<IEnumerable<Habit>> GetHabitsByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<Habit?> GetHabitByIdAsync(int id, CancellationToken cancellationToken);
}