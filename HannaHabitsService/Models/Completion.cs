namespace HannaHabitsService.Models;

public class Completion
{
    public int Id { get; set; }
    public int HabitId { get; set; }
    public Habit? Habit { get; set; }
    public DateTime? CompletionDate { get; set; }
}