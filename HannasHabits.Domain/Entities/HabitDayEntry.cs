using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.Entities;

/// <summary>
/// What is written down about one habit on one day, besides whether it was done: today the set of tags of that day
/// ("krank", "Urlaub"). Independent of the completion mark - a missed day can carry a tag, that is the point.
/// An entry is only stored while it holds something: the use cases remove it with its last tag.
/// The habit and the tags are other aggregates, so only their ids are held (reference by id).
/// </summary>
public class HabitDayEntry : EntityBase
{
    /// <summary>A day carries at most this many tags.</summary>
    public const int MaxTags = 10;

    // The same plausible range as the habit's start date: keeps absurd dates such as 0001-01-01 out of the store.
    public static readonly DateOnly MinDate = Habit.MinStartDate;
    public static readonly DateOnly MaxDate = Habit.MaxStartDate;

    public Guid HabitId { get; private set; }
    public DateOnly Date { get; private set; }

    private readonly List<HabitDayTag> _tags = new();
    public IReadOnlyCollection<HabitDayTag> Tags => _tags;

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private HabitDayEntry() { }

    private HabitDayEntry(Guid habitId, DateOnly date)
    {
        HabitId = habitId;
        Date = date;
    }

    /// <summary>A new, empty entry. The use case adds a tag right away: an entry is not stored empty.</summary>
    public static HabitDayEntry Create(Guid habitId, DateOnly date)
    {
        if (date < MinDate || date > MaxDate)
            throw new DomainException($"The day must be between {MinDate:O} and {MaxDate:O}.");

        return new HabitDayEntry(habitId, date);
    }

    public bool IsEmpty => _tags.Count == 0;

    /// <summary>Needs the tags loaded.</summary>
    public bool HasTag(Guid tagId) => _tags.Any(t => t.TagId == tagId);

    /// <summary>
    /// Puts a tag on the day. A tag can be on a day only once and a day holds at most <see cref="MaxTags"/>. Whether
    /// the tag exists and is the user's is the use case's business (it sees the tag repository). Needs the tags loaded.
    /// </summary>
    public void AddTag(Guid tagId)
    {
        if (HasTag(tagId))
            throw new DomainException("The day already has this tag.");

        if (_tags.Count >= MaxTags)
            throw new DomainException($"A day can have at most {MaxTags} tags.");

        _tags.Add(HabitDayTag.Create(Id, tagId));
    }

    /// <summary>Takes a tag off the day. Needs the tags loaded.</summary>
    public void RemoveTag(Guid tagId)
    {
        var tag = _tags.FirstOrDefault(t => t.TagId == tagId)
                  ?? throw new DomainException("The day does not have this tag.");

        _tags.Remove(tag);
    }

    /// <summary>
    /// What a merge of two tags does to a day: <paramref name="tagId"/> is taken off and <paramref name="replacementId"/>
    /// is on the day afterwards - added if it was not there yet, simply kept if it was (a day never carries a tag twice).
    /// Needs the tags loaded.
    /// </summary>
    public void ReplaceTag(Guid tagId, Guid replacementId)
    {
        if (tagId == replacementId)
            return;

        RemoveTag(tagId);

        if (!HasTag(replacementId))
            AddTag(replacementId);
    }
}
