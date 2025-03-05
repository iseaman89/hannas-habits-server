using System.ComponentModel.DataAnnotations;
using HannaHabitsService.Models;
using HannasHabits.Data.Shared.Enums;

namespace HannaHabitsService.Dtos.DailyDiaries;

public class DailyDiaryCreateDto
{
    [Required]
    public string UserId { get; set; }
    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public Mood Mood { get; set; }
    public float PhysicalMood { get; set; }
    public float MentalMood { get; set; }
    public string? Highlight { get; set; }
    public string[]? LearnedThings { get; set; }
    public string[]? GratefulThings { get; set; }
    public List<DailyTask>? DailyTasks { get; set; }
}