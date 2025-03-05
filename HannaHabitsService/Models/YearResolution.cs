namespace HannaHabitsService.Models;

public class YearResolution
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public int Year { get; set; }
    public List<Resolution>? Resolutions { get; set; }
    public string? Summary { get; set; }
}