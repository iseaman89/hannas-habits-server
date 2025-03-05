using System.ComponentModel.DataAnnotations;
using HannaHabitsService.Models;

namespace HannaHabitsService.Dtos.YearResolutions;

public class YearResolutionCreateDto
{
    [Required]
    public string? UserId { get; set; }
    [Required]
    public int Year { get; set; }
    public List<Resolution>? Resolutions { get; set; }
    public string? Summary { get; set; }
}