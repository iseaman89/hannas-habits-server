using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

/// <summary>
/// What the overview needs of a habit: its plan and every day it was completed. A plain data carrier from the read side;
/// the streak is not a column, the Domain computes it from these facts.
/// </summary>
public record HabitWithCompletedDates(
    Guid Id, string Title, HabitSchedule Schedule, DateOnly StartDate, IReadOnlyCollection<DateOnly> CompletedDates);
