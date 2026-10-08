using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;

public class DeleteDailyDiaryCommandHandler : IRequestHandler<DeleteDailyDiaryCommand, Unit>
{
    private readonly IDailyDiaryRepository _dailyDiaries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DeleteDailyDiaryCommandHandler(IDailyDiaryRepository dailyDiaries, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _dailyDiaries = dailyDiaries;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteDailyDiaryCommand request, CancellationToken cancellationToken)
    {
        var dailyDiary = await _dailyDiaries.GetByIdAsync(_currentUser.UserId, request.Id, cancellationToken);

        if (dailyDiary is null) throw new NotFoundException(nameof(DailyDiary), request.Id);

        _dailyDiaries.Remove(dailyDiary);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
