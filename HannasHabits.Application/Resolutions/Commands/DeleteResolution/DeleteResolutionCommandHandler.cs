using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.Resolutions.Commands.DeleteResolution;

public class DeleteResolutionCommandHandler : IRequestHandler<DeleteResolutionCommand, Unit>
{
    private readonly IResolutionRepository _resolutions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DeleteResolutionCommandHandler(IResolutionRepository resolutions, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _resolutions = resolutions;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteResolutionCommand request, CancellationToken cancellationToken)
    {
        var resolution = await _resolutions.GetByIdAsync(_currentUser.UserId, request.Year, request.Id, cancellationToken);

        if (resolution is null)
            throw new NotFoundException(nameof(Resolution), request.Id);

        _resolutions.Remove(resolution);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
