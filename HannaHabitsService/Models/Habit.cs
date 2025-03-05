using HannasHabits.Data.Shared.Enums;

namespace HannaHabitsService.Models;

public class Habit
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public Priority Priority { get; set; } = Priority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<DayOfWeek>? Schedules { get; set; }
    public List<Completion>? Completions { get; set; }
    public string? UserId { get; set; }
}