using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HannaHabitsService.Models;

public class Resolution
{
    public int Id { get; set; }
    public int YearResolutionsId { get; set; }
    public YearResolution? YearResolutions { get; set; }
    public string? Title { get; set; }
    public bool IsCompleted { get; set; }
}