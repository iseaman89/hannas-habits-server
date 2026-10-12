namespace HannasHabits.Application.Tags;

/// <summary>
/// Read side for tags and tagged days: projects straight to DTOs, nothing is tracked. Every method takes the owner's
/// id, data of other users is never returned.
/// </summary>
public interface ITagQueries
{
    /// <summary>
    /// The user's tags with their use. With a <paramref name="habitId"/> the habit's own use comes first: ordered by the
    /// days in that habit, then by the days overall, then by name. Without one: by days overall, then by name.
    /// <c>null</c> (not an empty list) if a habit was asked for that does not exist or belongs to another user.
    /// </summary>
    Task<List<TagDto>?> GetAllAsync(Guid userId, Guid? habitId, CancellationToken cancellationToken);

    /// <summary>
    /// Every tag on a day between <paramref name="from"/> and <paramref name="to"/> (inclusive) of any of the user's
    /// habits, one row per tag and day, ordered by date and then by tag name.
    /// </summary>
    Task<List<TaggedDayRow>> GetTaggedDaysAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>The tags of one habit on one day, by name; an untagged day is an empty list.</summary>
    Task<TaggedDayDto> GetTaggedDayAsync(Guid userId, Guid habitId, DateOnly date, CancellationToken cancellationToken);
}
