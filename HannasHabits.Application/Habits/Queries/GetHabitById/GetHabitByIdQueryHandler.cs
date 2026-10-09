using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetHabitById;

public class GetHabitByIdQueryHandler : IRequestHandler<GetHabitByIdQuery, HabitDetailsDto>
{
    private readonly IHabitQueries _habitQueries;
    private readonly ICurrentUser _currentUser;

    public GetHabitByIdQueryHandler(IHabitQueries habitQueries, ICurrentUser currentUser)
    {
        _habitQueries = habitQueries;
        _currentUser = currentUser;
    }

    public async Task<HabitDetailsDto> Handle(GetHabitByIdQuery request, CancellationToken cancellationToken)
    {
        var habit = await _habitQueries.GetByIdAsync(_currentUser.UserId, request.Id, cancellationToken);

        return habit ?? throw new NotFoundException(nameof(Habit), request.Id);
    }
}
