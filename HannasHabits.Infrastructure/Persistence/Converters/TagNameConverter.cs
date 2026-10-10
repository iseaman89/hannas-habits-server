using HannasHabits.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="TagName"/> as its plain text (<c>varchar</c>), in the spelling the person chose.</summary>
public class TagNameConverter : ValueConverter<TagName, string>
{
    public TagNameConverter() : base(name => name.Value, text => TagName.Create(text))
    {
    }
}
