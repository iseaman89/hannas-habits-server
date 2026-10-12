using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits;

/// <summary>
/// One day of a habit as it is after a tick: whether the habit itself is done that day and which of its sub-habits are.
/// Ticking a sub-habit can complete (or take back) the habit's day, so the answer carries both - a safe target for an
/// optimistic update, and the client never has to re-derive the rule.
/// </summary>
/// <param name="CompletedSubHabitIds">The sub-habits done that day (also one that was not due), in the order they were added.</param>
public record HabitDayDto(DateOnly Date, bool HabitCompleted, IReadOnlyList<Guid> CompletedSubHabitIds)
{
    /// <summary>The day as the aggregate has it. Needs the records and the sub-habits with their records loaded.</summary>
    public static HabitDayDto Of(Habit habit, DateOnly date) => new(
        date,
        habit.RecordOn(date) is not null,
        habit.SubHabits.Where(s => s.IsCompletedOn(date)).Select(s => s.Id).ToList());
}
