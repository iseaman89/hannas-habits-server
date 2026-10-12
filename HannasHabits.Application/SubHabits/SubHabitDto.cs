using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits;

/// <summary>A part of a habit ("Legs" of "Sport") and the weekdays it is planned on.</summary>
/// <param name="Weekdays">
/// The days of the week it is planned on (0 = Sunday ... 6 = Saturday), never empty, ascending. It is due where the
/// habit is due <i>and</i> the weekday is one of these.
/// </param>
public record SubHabitDto(Guid Id, string Title, IReadOnlyList<DayOfWeek> Weekdays)
{
    public static SubHabitDto From(SubHabit subHabit)
        => new(subHabit.Id, subHabit.Title.Value, subHabit.Weekdays.Days);
}
