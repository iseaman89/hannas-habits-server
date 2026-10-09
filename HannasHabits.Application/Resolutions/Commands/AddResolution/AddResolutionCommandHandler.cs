using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.Resolutions.Commands.AddResolution;

public class AddResolutionCommandHandler : IRequestHandler<AddResolutionCommand, ResolutionDto>
{
    private readonly IResolutionRepository _resolutions;
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public AddResolutionCommandHandler(
        IResolutionRepository resolutions, IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _resolutions = resolutions;
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ResolutionDto> Handle(AddResolutionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var habit = await LinkedHabitLookup.FindAsync(_habits, userId, request.HabitId, cancellationToken);
        var inYear = await _resolutions.CountForYearAsync(userId, request.Year, cancellationToken);

        // Two parallel requests can both see "49" and end up with 51. Accepted: the limit is a guard against runaway
        // data, not a business promise that would justify locking.
        var resolution = Resolution.Create(userId, request.Year, request.Title, habit?.Id, inYear);

        _resolutions.Add(resolution);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The habit is already loaded, so the response carries the "Tracked by ..." title without another query.
        return new ResolutionDto(resolution.Id, resolution.Title, resolution.Kept, resolution.HabitId, habit?.Title.Value);
    }
}
