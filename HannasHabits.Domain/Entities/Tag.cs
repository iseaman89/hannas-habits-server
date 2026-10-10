using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Entities;

/// <summary>
/// A short label of a user ("krank", "Urlaub") that can be put on a day of any of the user's habits. A tag belongs to
/// the user, not to a habit, so the same word works across habits. Names are unique per user, ignoring case.
/// Which days carry a tag is the business of <see cref="HabitDayEntry"/> (another aggregate, pointing here by id).
/// </summary>
public class Tag : EntityBase
{
    /// <summary>A user has at most this many tags.</summary>
    public const int MaxPerUser = 200;

    public Guid UserId { get; private set; }
    public TagName Name { get; private set; }

    /// <summary>
    /// <see cref="TagName.Key"/> of the name, kept as a column of its own: the unique index "one tag per name and user,
    /// ignoring case" and the lookup by name run on it. Changes only together with <see cref="Name"/>.
    /// </summary>
    public string NameKey { get; private set; }

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private Tag()
    {
        Name = null!;
        NameKey = null!;
    }

    private Tag(Guid userId, TagName name)
    {
        UserId = userId;
        Name = name;
        NameKey = name.Key;
    }

    /// <param name="tagsOfUser">
    /// How many tags the user already has. The limit spans several tags, which no single instance can know, so the
    /// caller supplies the fact and the Domain decides (same pattern as <see cref="Resolution.Create"/>).
    /// </param>
    public static Tag Create(Guid userId, TagName name, int tagsOfUser)
    {
        if (tagsOfUser >= MaxPerUser)
            throw new DomainException($"A user can have at most {MaxPerUser} tags.");

        return new Tag(userId, name);
    }

    /// <summary>
    /// Gives the tag another name (or another spelling of the same one). That no OTHER tag of the user already has the
    /// name is a fact about several tags - the use case looks it up and merges instead of renaming in that case.
    /// </summary>
    public void Rename(TagName name)
    {
        Name = name;
        NameKey = name.Key;
    }
}
