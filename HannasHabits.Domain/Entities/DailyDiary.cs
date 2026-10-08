using System.Diagnostics.CodeAnalysis;
using HannasHabits.Domain.Common;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Entities;

/// <summary>
/// The diary of one user for one day. The entry is one document that is replaced as a whole
/// (<see cref="Replace"/>); it is never stored without content.
/// </summary>
public class DailyDiary : EntityBase
{
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public Mood? Mood { get; private set; }
    public Percentage? Body { get; private set; }
    public Percentage? Mind { get; private set; }
    public string? Highlight { get; private set; }
    public IReadOnlyList<string> Grateful { get; private set; }
    public IReadOnlyList<string> Learned { get; private set; }
    public IReadOnlyList<DiaryTask> Tasks { get; private set; }

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private DailyDiary()
    {
        Grateful = [];
        Learned = [];
        Tasks = [];
    }

    private DailyDiary(Guid userId, DateOnly date, DiaryContent content)
    {
        UserId = userId;
        Date = date;
        Replace(content);
    }

    public static DailyDiary Create(Guid userId, DateOnly date, DiaryContent content)
        => new DailyDiary(userId, date, content);

    /// <summary>Replaces everything written for the day with <paramref name="content"/> (last write wins).</summary>
    [MemberNotNull(nameof(Grateful), nameof(Learned), nameof(Tasks))]
    public void Replace(DiaryContent content)
    {
        // "Nothing written" is no entry: the use case removes the diary instead of storing an empty one.
        if (content.IsEmpty)
            throw new DomainException("A diary entry must contain something.");

        Mood = content.Mood;
        Body = content.Body;
        Mind = content.Mind;
        Highlight = content.Highlight;
        Grateful = content.Grateful;
        Learned = content.Learned;
        Tasks = content.Tasks;
    }
}
