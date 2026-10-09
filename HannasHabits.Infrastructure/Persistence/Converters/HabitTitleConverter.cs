using HannasHabits.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="HabitTitle"/> as its plain text (<c>varchar</c>).</summary>
public class HabitTitleConverter : ValueConverter<HabitTitle, string>
{
    public HabitTitleConverter() : base(title => title.Value, text => HabitTitle.Create(text))
    {
    }
}
