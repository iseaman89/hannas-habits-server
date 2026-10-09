using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// One line of a day's task list: a trimmed, non-empty title of at most <see cref="TitleMaxLength"/> characters and a
/// done flag. Immutable; two tasks with the same title and state are equal (record equality). The position in the list
/// is the order of the user, it is not part of the task.
/// </summary>
public sealed record DiaryTask
{
    public const int TitleMaxLength = 200;

    public string Title { get; }
    public bool Done { get; }

    private DiaryTask(string title, bool done)
    {
        Title = title;
        Done = done;
    }

    public static DiaryTask Create(string? title, bool done)
    {
        var trimmed = title?.Trim();

        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("A task title must not be empty.");

        if (trimmed.Length > TitleMaxLength)
            throw new DomainException($"A task title must not be longer than {TitleMaxLength} characters.");

        return new DiaryTask(trimmed, done);
    }
}
