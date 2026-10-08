using HannasHabits.Domain.Common;

namespace HannasHabits.Domain.Entities;

public class HabitRecord : EntityBase
{
    public Guid HabitId { get; private set; }
    public DateOnly Date { get; private set; }

    private HabitRecord() { }

    private HabitRecord(Guid habitId, DateOnly date)
    {
        HabitId = habitId;
        Date = date;
    }

    // internal: a record is part of the Habit aggregate and may only be created through Habit.MarkCompleted, which
    // guards the "one record per day" rule.
    internal static HabitRecord Create(Guid habitId, DateOnly date)
        => new HabitRecord(habitId, date);
}
