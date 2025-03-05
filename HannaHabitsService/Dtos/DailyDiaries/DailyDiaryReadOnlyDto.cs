using HannaHabitsService.Models;
using HannasHabits.Data.Shared.Enums;

namespace HannaHabitsService.Dtos.DailyDiaries;

public class DailyDiaryReadOnlyDto : BaseDto
{
    public string? UserId { get; set; }
    public DateTime Date { get; set; }
    public Mood Mood { get; set; }
    public float PhysicalMood { get; set; }
    public float MentalMood { get; set; }
    public string? Highlight { get; set; }
    public string[]? LearnedThings { get; set; }
    public string[]? GratefulThings { get; set; }
    public List<DailyTask>? DailyTasks { get; set; }
}