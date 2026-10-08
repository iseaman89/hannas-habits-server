namespace HannasHabits.Application.Resolutions;

/// <param name="HabitId">The habit that tracks this resolution, or <c>null</c>.</param>
/// <param name="HabitTitle">Title of that habit (for the "Tracked by ..." line); <c>null</c> without a link.</param>
public record ResolutionDto(Guid Id, string Title, bool Kept, Guid? HabitId, string? HabitTitle);
