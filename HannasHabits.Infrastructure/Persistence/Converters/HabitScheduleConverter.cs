using HannasHabits.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a <see cref="HabitSchedule"/> as one <c>integer</c> with a bit per day (Sunday = bit 0 ... Saturday = bit 6),
/// e.g. Monday to Friday = 62. The encoding is a persistence detail: the Domain only exposes the days.
/// </summary>
public class HabitScheduleConverter : ValueConverter<HabitSchedule, int>
{
    /// <summary>Lowest and highest valid stored value: at least one day, all seven days at most.</summary>
    public const int MinMask = 1;
    public const int MaxMask = 0b111_1111;

    public HabitScheduleConverter() : base(schedule => ToMask(schedule), mask => FromMask(mask))
    {
    }

    public static int ToMask(HabitSchedule schedule)
        => schedule.Days.Aggregate(0, (mask, day) => mask | (1 << (int)day));

    public static HabitSchedule FromMask(int mask)
        => HabitSchedule.Create(Enum.GetValues<DayOfWeek>().Where(day => (mask & (1 << (int)day)) != 0));
}
