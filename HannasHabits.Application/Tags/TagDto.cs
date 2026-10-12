namespace HannasHabits.Application.Tags;

/// <summary>A tag with how much it is used.</summary>
/// <param name="UseCount">On how many days of all habits together the tag is set.</param>
/// <param name="HabitUseCount">On how many days of the habit that was asked for; 0 when no habit was asked for.</param>
/// <param name="LastUsedOn">The latest day of any habit that carries the tag; <c>null</c> for a tag that is not used.</param>
public record TagDto(Guid Id, string Name, int UseCount, int HabitUseCount, DateOnly? LastUsedOn);

/// <summary>A tag as it is shown on a day: just who it is.</summary>
public record TagSummaryDto(Guid Id, string Name);

/// <param name="Tags">The tags of the day by name; never empty in an answer that lists days.</param>
public record TaggedDayDto(DateOnly Date, IReadOnlyList<TagSummaryDto> Tags);

/// <summary>One tag on one day of one habit - what the read side hands to the use case that groups it.</summary>
public record TaggedDayRow(Guid HabitId, DateOnly Date, Guid TagId, string TagName);
