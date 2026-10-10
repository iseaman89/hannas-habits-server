using HannasHabits.Application.Tags;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Queries;

public class TagQueries : ITagQueries
{
    private readonly ApplicationDbContext _context;

    public TagQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TagDto>?> GetAllAsync(Guid userId, Guid? habitId, CancellationToken cancellationToken)
    {
        if (habitId is not null
            && !await _context.Habits.AnyAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken))
            return null;

        // Per tag three small correlated aggregates over its day rows. A user has at most 200 tags, so this stays small;
        // the index on HabitDayTags.TagId is what they run on.
        var rows = await _context.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.NameKey,
                UseCount = _context.HabitDayTags.Count(d => d.TagId == t.Id),
                HabitUseCount = habitId == null
                    ? 0
                    : _context.HabitDayTags.Count(d => d.TagId == t.Id
                                                       && _context.HabitDayEntries.Any(e => e.Id == d.HabitDayEntryId && e.HabitId == habitId)),
                LastUsedOn = _context.HabitDayEntries
                    .Where(e => e.Tags.Any(d => d.TagId == t.Id))
                    .Max(e => (DateOnly?)e.Date)
            })
            .OrderByDescending(row => row.HabitUseCount)
            .ThenByDescending(row => row.UseCount)
            .ThenBy(row => row.NameKey)
            .ToListAsync(cancellationToken);

        // The name is a value object: loaded as stored, the DTO is built in memory.
        return rows
            .Select(row => new TagDto(row.Id, row.Name.Value, row.UseCount, row.HabitUseCount, row.LastUsedOn))
            .ToList();
    }

    public async Task<List<TaggedDayRow>> GetTaggedDaysAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // The user filter sits on the habit AND on the tag: data isolation should not depend on a rule elsewhere.
        var rows = await (
                from dayTag in _context.HabitDayTags.AsNoTracking()
                join entry in _context.HabitDayEntries.AsNoTracking() on dayTag.HabitDayEntryId equals entry.Id
                join habit in _context.Habits.AsNoTracking() on entry.HabitId equals habit.Id
                join tag in _context.Tags.AsNoTracking() on dayTag.TagId equals tag.Id
                where habit.UserId == userId && tag.UserId == userId && entry.Date >= @from && entry.Date <= to
                orderby entry.Date, tag.NameKey, tag.Id
                select new { entry.HabitId, entry.Date, TagId = tag.Id, tag.Name })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new TaggedDayRow(row.HabitId, row.Date, row.TagId, row.Name.Value))
            .ToList();
    }

    public async Task<TaggedDayDto> GetTaggedDayAsync(
        Guid userId, Guid habitId, DateOnly date, CancellationToken cancellationToken)
    {
        var rows = await (
                from dayTag in _context.HabitDayTags.AsNoTracking()
                join entry in _context.HabitDayEntries.AsNoTracking() on dayTag.HabitDayEntryId equals entry.Id
                join habit in _context.Habits.AsNoTracking() on entry.HabitId equals habit.Id
                join tag in _context.Tags.AsNoTracking() on dayTag.TagId equals tag.Id
                where habit.UserId == userId && tag.UserId == userId && entry.HabitId == habitId && entry.Date == date
                orderby tag.NameKey, tag.Id
                select new { TagId = tag.Id, tag.Name })
            .ToListAsync(cancellationToken);

        return new TaggedDayDto(date, rows.Select(row => new TagSummaryDto(row.TagId, row.Name.Value)).ToList());
    }
}
