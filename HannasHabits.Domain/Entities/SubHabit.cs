using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Entities;

/// <summary>
/// A part of a habit - "Legs" and "Arms" of "Sport" - planned on some weekdays and ticked on its own. It lives inside
/// the <see cref="Habit"/> aggregate: whether it is due on a day depends on the habit's rule, and the habit's day
/// follows its sub-habits, both of which only the habit can see. So everything that changes a sub-habit goes through
/// the habit; this type only guards what concerns itself.
/// </summary>
public class SubHabit : EntityBase
{
    public Guid HabitId { get; private set; }
    public HabitTitle Title { get; private set; }

    /// <summary>
    /// The weekdays it is planned on. It is due on a day where the habit is due <i>and</i> the weekday is in this set,
    /// so a later change of the habit's rhythm carries the sub-habit along.
    /// </summary>
    public WeekdaySet Weekdays { get; private set; }

    private readonly List<SubHabitRecord> _records = new();
    public IReadOnlyCollection<SubHabitRecord> Records => _records;

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private SubHabit()
    {
        Title = null!;
        Weekdays = null!;
    }

    private SubHabit(Guid habitId, HabitTitle title, WeekdaySet weekdays)
    {
        HabitId = habitId;
        Title = title;
        Weekdays = weekdays;
    }

    internal static SubHabit Create(Guid habitId, HabitTitle title, WeekdaySet weekdays)
        => new SubHabit(habitId, title, weekdays);

    internal void Update(HabitTitle title, WeekdaySet weekdays)
    {
        Title = title;
        Weekdays = weekdays;
    }

    /// <summary>Whether the sub-habit was done on the day. Needs the records loaded.</summary>
    public bool IsCompletedOn(DateOnly date) => _records.Any(r => r.Date == date);

    internal SubHabitRecord MarkCompleted(DateOnly date)
    {
        if (IsCompletedOn(date))
            throw new DomainException($"The sub-habit is already completed on {date:O}.");

        var record = SubHabitRecord.Create(Id, date);
        _records.Add(record);
        return record;
    }

    internal void UnmarkCompleted(DateOnly date)
    {
        var record = _records.FirstOrDefault(r => r.Date == date)
                     ?? throw new DomainException($"The sub-habit is not completed on {date:O}.");

        _records.Remove(record);
    }
}
