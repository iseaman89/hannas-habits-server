namespace HannaHabitsService.Models;

public class DailyTask
{
    public int Id { get; set; }
    public int DailyDiaryId { get; set; }
    public DailyDiary? DailyDiary { get; set; }
    public string? Title { get; set; }
    public bool IsCompleted { get; set; }
}