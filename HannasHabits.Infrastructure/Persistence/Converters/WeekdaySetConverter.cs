using HannasHabits.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="WeekdaySet"/> as its bit set (<c>integer</c>, Sunday = bit 0).</summary>
public class WeekdaySetConverter : ValueConverter<WeekdaySet, int>
{
    public WeekdaySetConverter() : base(set => set.Mask, mask => WeekdaySet.FromMask(mask))
    {
    }
}
