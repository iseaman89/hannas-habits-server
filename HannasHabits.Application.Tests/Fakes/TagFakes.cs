using HannasHabits.Application.HabitDays;
using HannasHabits.Application.Tags;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tests.Fakes;

/// <summary>Holds the tags in a list; hands a tag out only to its owner. An added tag is stored at once.</summary>
internal sealed class FakeTagRepository : ITagRepository
{
    public List<Tag> Tags { get; } = [];
    public List<Tag> Removed { get; } = [];

    /// <summary>Overrides the answer of <see cref="CountAsync"/> (e.g. "the user has the most tags allowed").</summary>
    public int? CountOverride { get; set; }

    public Task<Tag?> GetByIdAsync(Guid userId, Guid tagId, CancellationToken cancellationToken)
        => Task.FromResult(Tags.FirstOrDefault(t => t.Id == tagId && t.UserId == userId));

    public Task<Tag?> FindByNameAsync(Guid userId, TagName name, CancellationToken cancellationToken)
        => Task.FromResult(Tags.FirstOrDefault(t => t.UserId == userId && t.NameKey == name.Key));

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(CountOverride ?? Tags.Count(t => t.UserId == userId));

    public void Add(Tag tag) => Tags.Add(tag);

    public void Remove(Tag tag)
    {
        Tags.Remove(tag);
        Removed.Add(tag);
    }
}

/// <summary>
/// Holds the day entries in a list. An entry belongs to a user through its habit, so the fake looks the habit up in the
/// habit repository, like the real query does.
/// </summary>
internal sealed class FakeHabitDayEntryRepository : IHabitDayEntryRepository
{
    private readonly FakeHabitRepository _habits;

    public FakeHabitDayEntryRepository(FakeHabitRepository habits)
    {
        _habits = habits;
    }

    public List<HabitDayEntry> Entries { get; } = [];
    public List<HabitDayEntry> Removed { get; } = [];

    private bool IsOwnedBy(HabitDayEntry entry, Guid userId)
        => _habits.Habits.Any(h => h.Id == entry.HabitId && h.UserId == userId);

    public Task<HabitDayEntry?> GetAsync(Guid userId, Guid habitId, DateOnly date, CancellationToken cancellationToken)
        => Task.FromResult(Entries.FirstOrDefault(e => e.HabitId == habitId && e.Date == date && IsOwnedBy(e, userId)));

    public Task<List<HabitDayEntry>> GetWithTagAsync(Guid userId, Guid tagId, CancellationToken cancellationToken)
        => Task.FromResult(Entries
            .Where(e => e.HasTag(tagId) && IsOwnedBy(e, userId))
            .OrderBy(e => e.Date)
            .ToList());

    public void Add(HabitDayEntry entry) => Entries.Add(entry);

    public void Remove(HabitDayEntry entry)
    {
        Entries.Remove(entry);
        Removed.Add(entry);
    }
}

/// <summary>The read side over the same lists; remembers which user it was asked for.</summary>
internal sealed class FakeTagQueries : ITagQueries
{
    private readonly FakeHabitRepository _habits;
    private readonly FakeTagRepository _tags;
    private readonly FakeHabitDayEntryRepository _days;

    public FakeTagQueries(FakeHabitRepository habits, FakeTagRepository tags, FakeHabitDayEntryRepository days)
    {
        _habits = habits;
        _tags = tags;
        _days = days;
    }

    public List<Guid> AskedForUsers { get; } = [];
    public Guid? AskedForHabit { get; private set; }
    public (DateOnly From, DateOnly To)? AskedForRange { get; private set; }

    public Task<List<TagDto>?> GetAllAsync(Guid userId, Guid? habitId, CancellationToken cancellationToken)
    {
        AskedForUsers.Add(userId);
        AskedForHabit = habitId;

        if (habitId is not null && !_habits.Habits.Any(h => h.Id == habitId && h.UserId == userId))
            return Task.FromResult<List<TagDto>?>(null);

        var owned = _days.Entries.Where(e => _habits.Habits.Any(h => h.Id == e.HabitId && h.UserId == userId)).ToList();

        // The order the contract promises: the habit's own use, the overall use, then the name.
        var result = _tags.Tags
            .Where(t => t.UserId == userId)
            .Select(t =>
            {
                var uses = owned.Where(e => e.HasTag(t.Id)).ToList();

                return new TagDto(
                    t.Id, t.Name.Value, uses.Count, uses.Count(e => e.HabitId == habitId),
                    uses.Count == 0 ? null : uses.Max(e => e.Date));
            })
            .OrderByDescending(t => t.HabitUseCount).ThenByDescending(t => t.UseCount).ThenBy(t => t.Name.ToUpperInvariant())
            .ToList();

        return Task.FromResult<List<TagDto>?>(result);
    }

    public Task<List<TaggedDayRow>> GetTaggedDaysAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        AskedForUsers.Add(userId);
        AskedForRange = (from, to);

        var rows = _days.Entries
            .Where(e => e.Date >= from && e.Date <= to && _habits.Habits.Any(h => h.Id == e.HabitId && h.UserId == userId))
            .SelectMany(e => e.Tags.Select(dayTag => (Entry: e, Tag: _tags.Tags.Single(t => t.Id == dayTag.TagId))))
            .OrderBy(x => x.Entry.Date).ThenBy(x => x.Tag.NameKey)
            .Select(x => new TaggedDayRow(x.Entry.HabitId, x.Entry.Date, x.Tag.Id, x.Tag.Name.Value))
            .ToList();

        return Task.FromResult(rows);
    }

    public Task<TaggedDayDto> GetTaggedDayAsync(Guid userId, Guid habitId, DateOnly date, CancellationToken cancellationToken)
    {
        AskedForUsers.Add(userId);

        var entry = _days.Entries.FirstOrDefault(e => e.HabitId == habitId && e.Date == date
                                                      && _habits.Habits.Any(h => h.Id == habitId && h.UserId == userId));

        var tags = (entry?.Tags ?? [])
            .Select(dayTag => _tags.Tags.Single(t => t.Id == dayTag.TagId))
            .OrderBy(t => t.NameKey)
            .Select(t => new TagSummaryDto(t.Id, t.Name.Value))
            .ToList();

        return Task.FromResult(new TaggedDayDto(date, tags));
    }
}
