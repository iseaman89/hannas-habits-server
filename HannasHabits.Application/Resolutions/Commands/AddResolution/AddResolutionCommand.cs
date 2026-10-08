using MediatR;

namespace HannasHabits.Application.Resolutions.Commands.AddResolution;

/// <summary>Adds a resolution (not kept yet) to a year, optionally tracked by one of the user's habits.</summary>
public record AddResolutionCommand(int Year, string Title, Guid? HabitId) : IRequest<ResolutionDto>;
