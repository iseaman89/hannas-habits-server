using HannasHabits.Domain.Common;

namespace HannasHabits.Domain.Entities;

/// <summary>One tag on one day: a part of the <see cref="HabitDayEntry"/> aggregate.</summary>
public class HabitDayTag : EntityBase
{
    public Guid HabitDayEntryId { get; private set; }
    public Guid TagId { get; private set; }

    private HabitDayTag() { }

    private HabitDayTag(Guid habitDayEntryId, Guid tagId)
    {
        HabitDayEntryId = habitDayEntryId;
        TagId = tagId;
    }

    // internal: part of the HabitDayEntry aggregate, created only through HabitDayEntry.AddTag, which guards "once per
    // day" and the limit.
    internal static HabitDayTag Create(Guid habitDayEntryId, Guid tagId)
        => new HabitDayTag(habitDayEntryId, tagId);
}
