using HannasHabits.Domain.Common;

namespace HannasHabits.Domain.Entities;

public class Habit : EntityBase
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }

    private readonly List<HabitRecord> _records = new();
    public IReadOnlyCollection<HabitRecord> Records => _records;

    private Habit() { }

    private Habit(Guid userId, string title, string? description)
    {
        UserId = userId;
        SetTitle(title);
        Description = description;
    }

    public static Habit Create(Guid userId, string title, string? description = null)
        => new Habit(userId, title, description);

    public void Update(string title, string? description = null)
    {
        Rename(title);
        UpdateDescription(description);
    }

    private void Rename(string title)
    {
        SetTitle(title);
    }

    private void UpdateDescription(string? desc)
    {
        Description = desc;
    }

    private void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Назва звички не може бути порожньою");

        Title = title.Trim();
    }

    public HabitRecord MarkCompleted(DateOnly date)
    {
        var record = HabitRecord.Create(Id, date);
        _records.Add(record);
        return record;
    }
}