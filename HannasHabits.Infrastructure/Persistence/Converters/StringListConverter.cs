using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores an ordered list of short texts as a native <c>text[]</c>. EF Core 8 cannot map a read-only list as a
/// primitive collection, so the list is converted to an array (which Npgsql maps to <c>text[]</c>) instead.
/// </summary>
public class StringListConverter : ValueConverter<IReadOnlyList<string>, string[]>
{
    public StringListConverter() : base(items => items.ToArray(), array => array)
    {
    }
}
