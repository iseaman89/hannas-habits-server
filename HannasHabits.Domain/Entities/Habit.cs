using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Entities;

public class Habit : EntityBase
{
    public const int DescriptionMaxLength = 500;

    public Guid UserId { get; private set; }
    public HabitTitle Title { get; private set; }
    public string? Description { get; private set; }
    public HabitSchedule Schedule { get; private set; }

    private readonly List<HabitRecord> _records = new();
    public IReadOnlyCollection<HabitRecord> Records => _records;

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private Habit()
    {
        Title = null!;
        Schedule = null!;
    }

    private Habit(Guid userId, HabitTitle title, string? description, HabitSchedule schedule)
    {
        UserId = userId;
        Title = title;
        Description = NormalizeDescription(description);
        Schedule = schedule;
    }

    /// <param name="schedule">The planned days; a habit without an explicit schedule is planned every day.</param>
    public static Habit Create(Guid userId, HabitTitle title, string? description = null, HabitSchedule? schedule = null)
        => new Habit(userId, title, description, schedule ?? HabitSchedule.Daily);

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
