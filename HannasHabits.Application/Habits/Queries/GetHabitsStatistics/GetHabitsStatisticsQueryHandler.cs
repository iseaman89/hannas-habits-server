using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;
using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Domain.Services;
using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetHabitsStatistics;

/// <summary>
/// Reads the same facts as the overview (every habit with all its completed days and those of its parts) and lets the
/// Domain's <see cref="HabitStatistics"/> count them: the handler only hands the dates over.
/// </summary>
public class GetHabitsStatisticsQueryHandler : IRequestHandler<GetHabitsStatisticsQuery, List<HabitStatisticsDto>>
{
    private readonly IHabitQueries _habitQueries;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetHabitsStatisticsQueryHandler(IHabitQueries habitQueries, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _habitQueries = habitQueries;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<List<HabitStatisticsDto>> Handle(GetHabitsStatisticsQuery request, CancellationToken cancellationToken)
    {
        // The validator guarantees From and To.
        var from = request.From!.Value;
        var to = request.To!.Value;
        var asOf = request.AsOf ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var habits = await _habitQueries.GetWithCompletedDatesAsync(_currentUser.UserId, cancellationToken);

        return habits.Select(habit => ToDto(habit, from, to, asOf)).ToList();
    }

    private static HabitStatisticsDto ToDto(HabitWithCompletedDates habit, DateOnly from, DateOnly to, DateOnly asOf)
    {
        // The whole history: "last done" and a week that reaches out of the range need days outside it.
        var completed = habit.CompletedDates.ToHashSet();
        var tally = HabitStatistics.Tally(habit.Schedule, habit.StartDate, completed, from, to, asOf);

        return new HabitStatisticsDto(
            habit.Id,
            habit.Title,
            RecurrenceDto.From(habit.Schedule),
            habit.StartDate,
            tally.Unit,
            tally.Planned,
            tally.Done,
            HabitStatistics.LastDone(habit.CompletedDates, asOf),
            habit.SubHabits.Select(part => PartOf(habit, part, from, to, asOf)).ToList());
    }

    private static SubHabitStatisticsDto PartOf(
        HabitWithCompletedDates habit, SubHabitWithCompletedDates part, DateOnly from, DateOnly to, DateOnly asOf)
    {
        var tally = HabitStatistics.Tally(
            habit.Schedule, habit.StartDate, part.Weekdays, part.CompletedDates.ToHashSet(), from, to, asOf);

        return new SubHabitStatisticsDto(
            part.Id, part.Title, part.Weekdays.Days, tally.Planned, tally.Done,
            HabitStatistics.LastDone(part.CompletedDates, asOf));
    }
}
