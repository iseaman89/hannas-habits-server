using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.Services;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Entities;

public class Habit : EntityBase
{
    public const int DescriptionMaxLength = 500;

    /// <summary>A habit has at most this many sub-habits.</summary>
    public const int MaxSubHabits = 10;

    // Plausible range for the start date (a future start is fine - "starting next Monday"); keeps absurd values such as
    // 0001-01-01 out of the grid and the streak walk.
    public static readonly DateOnly MinStartDate = new(2000, 1, 1);
    public static readonly DateOnly MaxStartDate = new(2100, 12, 31);

    public Guid UserId { get; private set; }
    public HabitTitle Title { get; private set; }
    public string? Description { get; private set; }
    public HabitSchedule Schedule { get; private set; }

    /// <summary>
    /// The first day the habit applies: earlier days are "not applicable" (never missed) and end a streak. A date chosen
    /// by the caller in their own time zone - not <see cref="EntityBase.CreatedAt"/>, a UTC timestamp that can be a day off.
    /// </summary>
    public DateOnly StartDate { get; private set; }

    private readonly List<HabitRecord> _records = new();
    public IReadOnlyCollection<HabitRecord> Records => _records;

    private readonly List<SubHabit> _subHabits = new();
    public IReadOnlyCollection<SubHabit> SubHabits => _subHabits;

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private Habit()
    {
        Title = null!;
        Schedule = null!;
    }

    private Habit(Guid userId, HabitTitle title, DateOnly startDate, string? description, HabitSchedule schedule)
    {
        UserId = userId;
        Title = title;
        StartDate = ValidateStartDate(startDate);
        Description = NormalizeDescription(description);
        Schedule = schedule;
    }

    /// <param name="startDate">The first day the habit applies, in the caller's time zone.</param>
    /// <param name="schedule">The planned days; a habit without an explicit schedule is planned every day.</param>
    public static Habit Create(
        Guid userId, HabitTitle title, DateOnly startDate, string? description = null, HabitSchedule? schedule = null)
        => new Habit(userId, title, startDate, description, schedule ?? HabitSchedule.Daily);

    /// <param name="startDate">
    /// A new first day, e.g. to back-fill earlier days; <c>null</c> keeps the current one. Records before a start date
    /// moved forward are kept but no longer shown or counted (like records on days that are no longer scheduled).
    /// </param>
    public void Update(HabitTitle title, string? description, HabitSchedule schedule, DateOnly? startDate = null)
    {
        // Validate first, assign afterwards: a rejected update must not leave the habit half changed.
        var normalizedDescription = NormalizeDescription(description);
        var validatedStartDate = startDate is { } date ? ValidateStartDate(date) : StartDate;

        Title = title;
        Description = normalizedDescription;
        Schedule = schedule;
        StartDate = validatedStartDate;
    }

    /// <summary>The record of the given day, or <c>null</c> if the habit was not completed that day. Needs the records loaded.</summary>
    public HabitRecord? RecordOn(DateOnly date)
        => _records.FirstOrDefault(r => r.Date == date);

    /// <summary>
    /// Marks a day as completed. A day can be completed only once. The habit's own tick ticks the sub-habits that are
    /// due that day too: "did Sport" means all of that day's parts. Needs the records and the sub-habits with their
    /// records loaded.
    /// </summary>
    public HabitRecord MarkCompleted(DateOnly date)
    {
        if (RecordOn(date) is not null)
            throw new DomainException($"The habit is already completed on {date:O}.");

        var record = HabitRecord.Create(Id, date);
        _records.Add(record);

        foreach (var subHabit in DueSubHabitsOn(date).Where(s => !s.IsCompletedOn(date)))
            subHabit.MarkCompleted(date);

        return record;
    }

    /// <summary>
    /// Takes back the completion of a day. Only a completed day can be taken back. The day's sub-habit marks go with
    /// it: a day that is not done has no part done. Needs the records and the sub-habits with their records loaded.
    /// </summary>
    public void UnmarkCompleted(DateOnly date)
    {
        var record = RecordOn(date)
                     ?? throw new DomainException($"The habit is not completed on {date:O}.");

        _records.Remove(record);

        foreach (var subHabit in _subHabits.Where(s => s.IsCompletedOn(date)))
            subHabit.UnmarkCompleted(date);
    }

    // ---- Sub-habits ----

    /// <summary>The sub-habit with the id, or <c>null</c>. Needs the sub-habits loaded.</summary>
    public SubHabit? SubHabitById(Guid subHabitId)
        => _subHabits.FirstOrDefault(s => s.Id == subHabitId);

    /// <summary>
    /// Adds a part of the habit. The title must be free among the habit's sub-habits (ignoring case), and the weekdays
    /// must be days the habit itself is planned on - "Legs on Tuesday" makes no sense for a Monday-Wednesday-Friday
    /// habit. Needs the sub-habits loaded.
    /// </summary>
    public SubHabit AddSubHabit(HabitTitle title, WeekdaySet weekdays)
    {
        if (_subHabits.Count >= MaxSubHabits)
            throw new DomainException($"A habit can have at most {MaxSubHabits} sub-habits.");

        EnsureTitleIsFree(title, except: null);
        EnsureWeekdaysAreOfTheHabit(weekdays);

        var subHabit = SubHabit.Create(Id, title, weekdays);
        _subHabits.Add(subHabit);
        return subHabit;
    }

    /// <summary>
    /// Renames a sub-habit and / or moves it to other weekdays. The weekdays are checked against the habit's days only
    /// where they change, so a sub-habit that fell out of step after the habit's rhythm changed can still be renamed.
    /// Needs the sub-habits loaded.
    /// </summary>
    public void UpdateSubHabit(Guid subHabitId, HabitTitle title, WeekdaySet weekdays)
    {
        var subHabit = SubHabitOrThrow(subHabitId);

        // Validate first, assign afterwards: a rejected update must not leave the sub-habit half changed.
        EnsureTitleIsFree(title, except: subHabit);

        if (weekdays != subHabit.Weekdays)
            EnsureWeekdaysAreOfTheHabit(weekdays);

        subHabit.Update(title, weekdays);
    }

    /// <summary>
    /// Removes a sub-habit with all its marks. The habit's own days are left as they are. Needs the sub-habits loaded.
    /// </summary>
    public void RemoveSubHabit(Guid subHabitId)
        => _subHabits.Remove(SubHabitOrThrow(subHabitId));

    /// <summary>
    /// Whether the sub-habit is due on the day: the habit is due and the weekday is one of the sub-habit's.
    /// </summary>
    public bool IsDueOn(SubHabit subHabit, DateOnly date)
        => HabitDueDays.IsDue(Schedule, StartDate, subHabit.Weekdays, date);

    /// <summary>The sub-habits that are due on the day, in the order they were added. Needs the sub-habits loaded.</summary>
    public IReadOnlyList<SubHabit> DueSubHabitsOn(DateOnly date)
        => _subHabits.Where(s => IsDueOn(s, date)).ToList();

    /// <summary>
    /// Marks a sub-habit as done on a day. A sub-habit can be done on a day only once. The habit's day follows: when
    /// this was the last of the day's due sub-habits, the habit is completed that day too. Marking is permissive like
    /// the habit's own - a day the sub-habit is not due on can be ticked (the UI blocks it); such a mark is only a
    /// mark and never completes the habit. Needs the records and the sub-habits with their records loaded.
    /// </summary>
    public SubHabitRecord MarkSubHabitCompleted(Guid subHabitId, DateOnly date)
    {
        var subHabit = SubHabitOrThrow(subHabitId);
        var record = subHabit.MarkCompleted(date);

        if (RecordOn(date) is null && IsDueOn(subHabit, date) && DueSubHabitsOn(date).All(s => s.IsCompletedOn(date)))
            _records.Add(HabitRecord.Create(Id, date));

        return record;
    }

    /// <summary>
    /// Takes back a sub-habit's mark. The habit's day follows: a day that was done only because all its due sub-habits
    /// were is not done any more. Only a done sub-habit can be taken back. Needs the records and the sub-habits with
    /// their records loaded.
    /// </summary>
    public void UnmarkSubHabitCompleted(Guid subHabitId, DateOnly date)
    {
        var subHabit = SubHabitOrThrow(subHabitId);
        subHabit.UnmarkCompleted(date);

        if (IsDueOn(subHabit, date) && RecordOn(date) is { } record)
            _records.Remove(record);
    }

    private SubHabit SubHabitOrThrow(Guid subHabitId)
        => SubHabitById(subHabitId) ?? throw new DomainException("The habit has no such sub-habit.");

    private void EnsureTitleIsFree(HabitTitle title, SubHabit? except)
    {
        var taken = _subHabits.Any(s => s != except
                                        && string.Equals(s.Title.Value, title.Value, StringComparison.OrdinalIgnoreCase));

        if (taken)
            throw new DomainException($"The habit already has a sub-habit called '{title.Value}'.");
    }

    private void EnsureWeekdaysAreOfTheHabit(WeekdaySet weekdays)
    {
        if (!weekdays.IsSubsetOf(WeekdaySet.Of(Schedule.Days)))
            throw new DomainException("A sub-habit can only be planned on days the habit itself is planned on.");
    }

    private static DateOnly ValidateStartDate(DateOnly startDate)
        => startDate < MinStartDate || startDate > MaxStartDate
            ? throw new DomainException($"The start date must be between {MinStartDate:O} and {MaxStartDate:O}.")
            : startDate;

    // An empty or whitespace-only description means "no description".
    private static string? NormalizeDescription(string? description)
    {
        var normalized = description?.Trim();

        if (string.IsNullOrEmpty(normalized))
            return null;

        if (normalized.Length > DescriptionMaxLength)
            throw new DomainException($"A habit description must not be longer than {DescriptionMaxLength} characters.");

        return normalized;
    }
}
