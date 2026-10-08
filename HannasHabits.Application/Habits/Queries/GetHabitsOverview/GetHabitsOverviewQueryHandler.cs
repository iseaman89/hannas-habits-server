using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Services;
using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

public class GetHabitsOverviewQueryHandler : IRequestHandler<GetHabitsOverviewQuery, List<HabitOverviewDto>>
{
    private readonly IHabitQueries _habitQueries;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetHabitsOverviewQueryHandler(IHabitQueries habitQueries, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _habitQueries = habitQueries;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<List<HabitOverviewDto>> Handle(GetHabitsOverviewQuery request, CancellationToken cancellationToken)
    {
        // The validator guarantees From and To.
        var from = request.From!.Value;
        var to = request.To!.Value;
        var asOf = request.AsOf ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var habits = await _habitQueries.GetWithCompletedDatesAsync(_currentUser.UserId, cancellationToken);

        return habits
            .Select(habit => new HabitOverviewDto(
                habit.Id,
                habit.Title,
                habit.Schedule.Days,
                habit.StartDate,
                habit.CompletedDates.Where(date => date >= from && date <= to).Order().ToList(),
                HabitStreak.Current(habit.Schedule, habit.StartDate, habit.CompletedDates.ToHashSet(), asOf)))
            .ToList();
    }
}
