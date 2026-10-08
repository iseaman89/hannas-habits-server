using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.Entities;

/// <summary>
/// One resolution of a user for one year ("Read 12 books"), kept or not yet. It may point to a habit that tracks it;
/// the habit is another aggregate, so only its id is held (reference by id): deleting the habit must not delete or break
/// the resolution, it just loses the link.
/// </summary>
public class Resolution : EntityBase
{
    public const int TitleMaxLength = 200;

    /// <summary>A year holds at most this many resolutions.</summary>
    public const int MaxPerYear = 50;

    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public Guid UserId { get; private set; }
    public int Year { get; private set; }
    public string Title { get; private set; }
    public bool Kept { get; private set; }
    public Guid? HabitId { get; private set; }

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private Resolution()
    {
        Title = null!;
    }

    private Resolution(Guid userId, int year, string title, Guid? habitId)
    {
        UserId = userId;
        Year = year;
        Title = title;
        HabitId = habitId;
    }

    /// <param name="resolutionsInYear">
    /// How many resolutions the user already has in that year. The limit spans several resolutions, which no single
    /// instance can know, so the caller supplies the fact and the Domain decides.
    /// </param>
    public static Resolution Create(Guid userId, int year, string? title, Guid? habitId, int resolutionsInYear)
    {
        if (year is < MinYear or > MaxYear)
            throw new DomainException($"The year of a resolution must be between {MinYear} and {MaxYear}.");

        if (resolutionsInYear >= MaxPerYear)
            throw new DomainException($"A year can have at most {MaxPerYear} resolutions.");

        return new Resolution(userId, year, NormalizeTitle(title), habitId);
    }

    /// <summary>Replaces everything that can change; the year and the owner are fixed for good.</summary>
    public void Update(string? title, bool kept, Guid? habitId)
    {
        Title = NormalizeTitle(title);
        Kept = kept;
        HabitId = habitId;
    }

    private static string NormalizeTitle(string? title)
    {
        var normalized = title?.Trim();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException("A resolution title must not be empty.");

        if (normalized.Length > TitleMaxLength)
            throw new DomainException($"A resolution title must not be longer than {TitleMaxLength} characters.");

        return normalized;
    }
}
