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
    private readonly IHabitQueries _habitQueries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public MarkCompletedCommandHandler(IHabitRepository habits, IHabitQueries habitQueries, IUnitOfWork unitOfWork,
        ICurrentUser currentUser, IMapper mapper)
    {
        _habits = habits;
        _habitQueries = habitQueries;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<HabitRecordDto> Handle(MarkCompletedCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var habit = await _habits.GetByIdWithRecordsAsync(userId, request.HabitId, cancellationToken);

        if (habit is null)
            throw new NotFoundException(nameof(Habit), request.HabitId);

        // The Domain forbids marking a day twice; making the API call idempotent is this use case's decision:
        // marking an already completed day is not an error, the client just gets the existing record.
        var existing = habit.RecordOn(request.Date);
        if (existing is not null)
            return _mapper.Map<HabitRecordDto>(existing);

        var record = habit.MarkCompleted(request.Date);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEntryException)
        {
            // A concurrent request marked the same day between our load and our save and the unique index
            // rejected ours. The goal "the day is completed" is reached either way: answer with the winner's record.
            var records = await _habitQueries.GetRecordsAsync(userId, request.HabitId, request.Date, request.Date, cancellationToken);
            var winner = records?.SingleOrDefault();

            if (winner is null)
                throw; // the winner was deleted again in the meantime: let the client retry (409)

            return winner;
        }

        return _mapper.Map<HabitRecordDto>(record);
    }
}
