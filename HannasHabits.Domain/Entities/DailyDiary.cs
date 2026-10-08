using System.Diagnostics.CodeAnalysis;
using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.Entities;

public class DailyDiary : EntityBase
{
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Text { get; private set; }

    // Parameterless constructor for EF Core only; it overwrites the value after materialization.
    private DailyDiary() { Text = null!; }

    private DailyDiary(Guid userId, DateOnly date, string text)
    {
        UserId = userId;
        Date = date;
        SetText(text);
    }

    public static DailyDiary Create(Guid userId, DateOnly date, string text)
        => new DailyDiary(userId, date, text);

    public void Update(string newText)
    {
        SetText(newText);
    }

    [MemberNotNull(nameof(Text))]
    private void SetText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("A diary text must not be empty.");

        Text = text.Trim();
    }
}