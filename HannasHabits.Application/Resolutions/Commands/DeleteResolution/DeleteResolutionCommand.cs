using MediatR;

namespace HannasHabits.Application.Resolutions.Commands.DeleteResolution;

public record DeleteResolutionCommand(int Year, Guid Id) : IRequest<Unit>;
