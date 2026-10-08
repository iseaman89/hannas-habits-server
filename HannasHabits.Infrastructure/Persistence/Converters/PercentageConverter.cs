using HannasHabits.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="Percentage"/> as its plain number (<c>integer</c>).</summary>
public class PercentageConverter : ValueConverter<Percentage, int>
{
    public PercentageConverter() : base(percentage => percentage.Value, value => Percentage.Create(value))
    {
    }
}
