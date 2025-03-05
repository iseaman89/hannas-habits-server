using System.ComponentModel.DataAnnotations;
using HannaHabitsService.Models;

namespace HannaHabitsService.Dtos.Completions;

public class CompletionCreateDto : BaseDto
{
    [Required]
    public int HabitId { get; set; }
    
    public Habit? Habit { get; set; }
    [Required]
    public DateTime? CompletionDate { get; set; }
}