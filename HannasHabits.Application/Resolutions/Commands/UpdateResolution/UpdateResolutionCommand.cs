using MediatR;

namespace HannasHabits.Application.Resolutions.Commands.UpdateResolution;

/// <summary>
/// Replaces everything that can change on a resolution (title, kept, habit link); leaving the habit out removes the
/// link. <paramref name="Kept"/> is nullable only so that a missing value is an error instead of a silent "not kept".
/// </summary>
public record UpdateResolutionCommand(int Year, Guid Id, string Title, bool? Kept, Guid? HabitId) : IRequest<Unit>;
