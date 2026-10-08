using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Entities;

public class Habit : EntityBase
{
    public const int DescriptionMaxLength = 500;

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

    public void Update(HabitTitle title, string? description, HabitSchedule schedule)
    {
        Title = title;
        Description = NormalizeDescription(description);
        Schedule = schedule;
    }

    /// <summary>The record of the given day, or <c>null</c> if the habit was not completed that day. Needs the records loaded.</summary>
    public HabitRecord? RecordOn(DateOnly date)
        => _records.FirstOrDefault(r => r.Date == date);

    /// <summary>Marks a day as completed. A day can be completed only once. Needs the records loaded.</summary>
    public HabitRecord MarkCompleted(DateOnly date)
    {
        if (RecordOn(date) is not null)
            throw new DomainException($"The habit is already completed on {date:O}.");

        var record = HabitRecord.Create(Id, date);
        _records.Add(record);
        return record;
    }

    /// <summary>Takes back the completion of a day. Only a completed day can be taken back. Needs the records loaded.</summary>
    public void UnmarkCompleted(DateOnly date)
    {
        var record = RecordOn(date)
                     ?? throw new DomainException($"The habit is not completed on {date:O}.");

        _records.Remove(record);
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
