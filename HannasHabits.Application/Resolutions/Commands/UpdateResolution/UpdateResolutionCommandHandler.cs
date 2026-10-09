using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.Resolutions.Commands.UpdateResolution;

public class UpdateResolutionCommandHandler : IRequestHandler<UpdateResolutionCommand, Unit>
{
    private readonly IResolutionRepository _resolutions;
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UpdateResolutionCommandHandler(
        IResolutionRepository resolutions, IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _resolutions = resolutions;
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateResolutionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var resolution = await _resolutions.GetByIdAsync(userId, request.Year, request.Id, cancellationToken);

        if (resolution is null)
            throw new NotFoundException(nameof(Resolution), request.Id);

        var habit = await LinkedHabitLookup.FindAsync(_habits, userId, request.HabitId, cancellationToken);

        // The validator guarantees a value; the pipeline runs it before this handler.
        resolution.Update(request.Title, request.Kept!.Value, habit?.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
