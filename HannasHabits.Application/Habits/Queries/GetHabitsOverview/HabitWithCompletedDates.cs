using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

/// <summary>
/// What the overview needs of a habit: its plan, every day it was completed and its sub-habits with theirs. A plain
/// data carrier from the read side; the streak is not a column, the Domain computes it from these facts.
/// </summary>
public record HabitWithCompletedDates(
    Guid Id, string Title, HabitSchedule Schedule, DateOnly StartDate, IReadOnlyCollection<DateOnly> CompletedDates,
    IReadOnlyCollection<SubHabitWithCompletedDates> SubHabits);

/// <summary>A sub-habit with every day it was done, as the read side hands it over (in the order they were added).</summary>
public record SubHabitWithCompletedDates(
    Guid Id, string Title, WeekdaySet Weekdays, IReadOnlyCollection<DateOnly> CompletedDates);
