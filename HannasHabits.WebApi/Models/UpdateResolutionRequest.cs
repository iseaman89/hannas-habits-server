using HannasHabits.Application.Resolutions.Commands.UpdateResolution;

namespace HannasHabits.WebApi.Models;

/// <summary>The whole resolution: an update replaces title, kept flag and habit link.</summary>
/// <param name="Kept">Required (not defaulted to false), so a forgotten value cannot re-open a kept resolution.</param>
/// <param name="HabitId">Optional; left out = no habit linked.</param>
public record UpdateResolutionRequest(string Title, bool? Kept, Guid? HabitId)
{
    public UpdateResolutionCommand ToCommand(int year, Guid id) => new(year, id, Title, Kept, HabitId);
}
