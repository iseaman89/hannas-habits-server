using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Application.SubHabits;
using HannasHabits.Application.Tags;
using HannasHabits.Domain.Services;
using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

public class GetHabitsOverviewQueryHandler : IRequestHandler<GetHabitsOverviewQuery, List<HabitOverviewDto>>
{
    private readonly IHabitQueries _habitQueries;
    private readonly ITagQueries _tagQueries;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetHabitsOverviewQueryHandler(
        IHabitQueries habitQueries, ITagQueries tagQueries, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _habitQueries = habitQueries;
        _tagQueries = tagQueries;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<List<HabitOverviewDto>> Handle(GetHabitsOverviewQuery request, CancellationToken cancellationToken)
    {
        // The validator guarantees From and To.
        var from = request.From!.Value;
        var to = request.To!.Value;
        var asOf = request.AsOf ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var userId = _currentUser.UserId;
        var habits = await _habitQueries.GetWithCompletedDatesAsync(userId, cancellationToken);

        // One query for the tags of the whole range and all habits, grouped here: not one per habit.
        var taggedDays = (await _tagQueries.GetTaggedDaysAsync(userId, from, to, cancellationToken))
            .ToLookup(row => row.HabitId);

        // The same for the parts' notes: one query for the range, handed to each part by its id.
        var notes = (await _habitQueries.GetSubHabitNotesAsync(userId, from, to, cancellationToken))
            .ToLookup(row => row.SubHabitId);

        return habits.Select(habit => ToDto(habit, from, to, asOf, taggedDays[habit.Id], notes)).ToList();
    }

    private static HabitOverviewDto ToDto(
        HabitWithCompletedDates habit, DateOnly from, DateOnly to, DateOnly asOf, IEnumerable<TaggedDayRow> taggedDays,
        ILookup<Guid, SubHabitNoteRow> notes)
    {
        // The whole history: the streak and a week that reaches out of the range need days outside it.
        var completed = habit.CompletedDates.ToHashSet();

        return new HabitOverviewDto(
            habit.Id,
            habit.Title,
            RecurrenceDto.From(habit.Schedule),
            habit.StartDate,
            HabitDueDays.Between(habit.Schedule, habit.StartDate, from, to),
            habit.CompletedDates.Where(date => date >= from && date <= to).Order().ToList(),
            TaggedDaysOf(taggedDays),
            HabitQuota.Between(habit.Schedule, habit.StartDate, completed, from, to, asOf)
                .Select(progress => new QuotaPeriodDto(progress.Period.From, progress.Period.To, progress.Done))
                .ToList(),
            HabitStreak.Current(habit.Schedule, habit.StartDate, completed, asOf),
            habit.SubHabits.Select(subHabit => SubHabitOf(habit, subHabit, from, to, notes[subHabit.Id])).ToList());
    }

    private static SubHabitOverviewDto SubHabitOf(
        HabitWithCompletedDates habit, SubHabitWithCompletedDates subHabit, DateOnly from, DateOnly to,
        IEnumerable<SubHabitNoteRow> notes)
        => new(
            subHabit.Id,
            subHabit.Title,
            subHabit.Weekdays.Days,
            HabitDueDays.Between(habit.Schedule, habit.StartDate, subHabit.Weekdays, from, to),
            subHabit.CompletedDates.Where(date => date >= from && date <= to).Order().ToList(),
            notes.OrderBy(row => row.Date).Select(row => new SubHabitNoteDto(row.Date, row.Text)).ToList());

    // The rows come ordered by date and tag name; grouping keeps that order.
    private static List<TaggedDayDto> TaggedDaysOf(IEnumerable<TaggedDayRow> rows)
        => rows
            .GroupBy(row => row.Date)
            .Select(day => new TaggedDayDto(
                day.Key, day.Select(row => new TagSummaryDto(row.TagId, row.TagName)).ToList()))
            .ToList();
}
