using HannasHabits.Domain.Common;

namespace HannasHabits.Domain.Entities;

/// <summary>One day on which a sub-habit was done.</summary>
public class SubHabitRecord : EntityBase
{
    public Guid SubHabitId { get; private set; }
    public DateOnly Date { get; private set; }

    private SubHabitRecord() { }

    private SubHabitRecord(Guid subHabitId, DateOnly date)
    {
        SubHabitId = subHabitId;
        Date = date;
    }

    // internal: a record is part of the Habit aggregate and may only be created through Habit, which guards the
    // "one record per day" rule and keeps the habit's own day in step.
    internal static SubHabitRecord Create(Guid subHabitId, DateOnly date)
        => new SubHabitRecord(subHabitId, date);
}
