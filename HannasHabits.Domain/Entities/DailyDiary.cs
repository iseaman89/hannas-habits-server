using HannasHabits.Domain.Common;

namespace HannasHabits.Domain.Entities;

public class DailyDiary : EntityBase
{
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Text { get; private set; }

    private DailyDiary() { }

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

    private void SetText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Текст не може бути пустим");

        Text = text.Trim();
    }
}