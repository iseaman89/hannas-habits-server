using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// Everything a user writes down for one day: mood, body and mind, a highlight, two short lists and the tasks. Every
/// part is optional, but the parts are valid and normalized (trimmed); an instance can only exist if it is valid.
/// <para>
/// It is the unit the diary is saved in (the client always sends the whole document), and it exists on its own so that
/// "is there anything to keep?" (<see cref="IsEmpty"/>) can be answered before a <c>DailyDiary</c> exists.
/// A class instead of a record on purpose: record equality would compare the lists by reference, and nobody needs to
/// compare two contents.
/// </para>
/// </summary>
public sealed class DiaryContent
{
    public const int HighlightMaxLength = 5000;

    /// <summary>Longest entry of the grateful and learned lists.</summary>
    public const int ItemMaxLength = 200;

    public const int MaxItemsPerList = 30;
    public const int MaxTasks = 50;

    public Mood? Mood { get; }
    public Percentage? Body { get; }
    public Percentage? Mind { get; }
    public string? Highlight { get; }
    public IReadOnlyList<string> Grateful { get; }
    public IReadOnlyList<string> Learned { get; }
    public IReadOnlyList<DiaryTask> Tasks { get; }

    /// <summary>Nothing is written down at all.</summary>
    public bool IsEmpty => Mood is null && Body is null && Mind is null && Highlight is null
                           && Grateful.Count == 0 && Learned.Count == 0 && Tasks.Count == 0;

    private DiaryContent(Mood? mood, Percentage? body, Percentage? mind, string? highlight,
        IReadOnlyList<string> grateful, IReadOnlyList<string> learned, IReadOnlyList<DiaryTask> tasks)
    {
        Mood = mood;
        Body = body;
        Mind = mind;
        Highlight = highlight;
        Grateful = grateful;
        Learned = learned;
        Tasks = tasks;
    }

    /// <param name="highlight">Blank means "no highlight".</param>
    /// <param name="grateful">Entries in the user's order; no entry may be blank. <c>null</c> means none.</param>
    /// <param name="learned">Same rules as <paramref name="grateful"/>.</param>
    /// <param name="tasks">Tasks in the user's order. <c>null</c> means none.</param>
    public static DiaryContent Create(Mood? mood, Percentage? body, Percentage? mind, string? highlight,
        IEnumerable<string>? grateful, IEnumerable<string>? learned, IEnumerable<DiaryTask>? tasks)
    {
        if (mood is { } value && !Enum.IsDefined(value))
            throw new DomainException($"'{(int)value}' is not a mood.");

        return new DiaryContent(
            mood, body, mind,
            NormalizeHighlight(highlight),
            NormalizeItems(grateful, "grateful"),
            NormalizeItems(learned, "learned"),
            NormalizeTasks(tasks));
    }

    // An empty or whitespace-only highlight means "no highlight".
    private static string? NormalizeHighlight(string? highlight)
    {
        var normalized = highlight?.Trim();

        if (string.IsNullOrEmpty(normalized))
            return null;

        if (normalized.Length > HighlightMaxLength)
            throw new DomainException($"A highlight must not be longer than {HighlightMaxLength} characters.");

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeItems(IEnumerable<string>? items, string listName)
    {
        var list = items?.ToArray() ?? [];

        if (list.Length > MaxItemsPerList)
            throw new DomainException($"The '{listName}' list must not contain more than {MaxItemsPerList} entries.");

        return list.Select(item => NormalizeItem(item, listName)).ToArray();
    }

    private static string NormalizeItem(string? item, string listName)
    {
        var normalized = item?.Trim();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException($"An entry of the '{listName}' list must not be empty.");

        if (normalized.Length > ItemMaxLength)
            throw new DomainException($"An entry of the '{listName}' list must not be longer than {ItemMaxLength} characters.");

        return normalized;
    }

    private static IReadOnlyList<DiaryTask> NormalizeTasks(IEnumerable<DiaryTask>? tasks)
    {
        var list = tasks?.ToArray() ?? [];

        if (list.Length > MaxTasks)
            throw new DomainException($"A day must not have more than {MaxTasks} tasks.");

        return list;
    }
}
