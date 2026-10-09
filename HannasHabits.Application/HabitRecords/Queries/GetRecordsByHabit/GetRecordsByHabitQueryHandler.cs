using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;

public class GetRecordsByHabitQueryHandler : IRequestHandler<GetRecordsByHabitQuery, List<HabitRecordDto>>
{
    private readonly IHabitQueries _habitQueries;
    private readonly ICurrentUser _currentUser;

    public GetRecordsByHabitQueryHandler(IHabitQueries habitQueries, ICurrentUser currentUser)
    {
        _habitQueries = habitQueries;
        _currentUser = currentUser;
    }

    public async Task<List<HabitRecordDto>> Handle(GetRecordsByHabitQuery request, CancellationToken cancellationToken)
    {
        var records = await _habitQueries.GetRecordsAsync(
            _currentUser.UserId, request.HabitId, request.From, request.To, cancellationToken);

        return records ?? throw new NotFoundException(nameof(Habit), request.HabitId);
    }
}
