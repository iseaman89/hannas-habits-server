using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MapsterMapper;
using MediatR;

namespace HannasHabits.Application.HabitRecords.Commands.MarkCompleted;

public class MarkCompletedCommandHandler : IRequestHandler<MarkCompletedCommand, HabitRecordDto>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public MarkCompletedCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser, IMapper mapper)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<HabitRecordDto> Handle(MarkCompletedCommand request, CancellationToken cancellationToken)
    {
        var habit = await _habits.GetByIdWithRecordsAsync(_currentUser.UserId, request.HabitId, cancellationToken);

        if (habit is null)
            throw new NotFoundException(nameof(Habit), request.HabitId);

        // Idempotent: marking an already completed day is not an error, the client just gets the existing record.
        var record = habit.Records.FirstOrDefault(r => r.Date == request.Date)
                     ?? habit.MarkCompleted(request.Date);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<HabitRecordDto>(record);
    }
}
