using HannasHabits.Application.Common.Interfaces;
using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetAllHabits;

public class GetAllHabitsQueryHandler
    : IRequestHandler<GetAllHabitsQuery, List<HabitListItemDto>>
{
    private readonly IHabitQueries _habitQueries;
    private readonly ICurrentUser _currentUser;

    public GetAllHabitsQueryHandler(IHabitQueries habitQueries, ICurrentUser currentUser)
    {
        _habitQueries = habitQueries;
        _currentUser = currentUser;
    }

    public Task<List<HabitListItemDto>> Handle(GetAllHabitsQuery request, CancellationToken cancellationToken)
        => _habitQueries.GetAllAsync(_currentUser.UserId, cancellationToken);
}
