using System.ComponentModel.DataAnnotations;
using HannaHabitsService.Models;
using HannasHabits.Data.Shared.Enums;

namespace HannaHabitsService.Dtos.Habits;

public class HabitCreateDto
{
    [Required]
    [StringLength(100)]
    public string? Title { get; set; }

    [Required]
    public Priority Priority { get; set; } = Priority.Normal;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public List<DayOfWeek>? Schedules { get; set; }
    
    public List<Completion>? Completions { get; set; }
    
    [Required]
    public string? UserId { get; set; }
}