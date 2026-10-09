namespace HannasHabits.Application.Resolutions;

/// <summary>
/// Read side for resolutions: projects straight to DTOs, nothing is tracked. Every method takes the owner's id,
/// data of other users is never returned.
/// </summary>
public interface IResolutionQueries
{
    /// <summary>The user's resolutions of a year in creation order, with the title of the linked habit. Empty if there are none.</summary>
    Task<List<ResolutionDto>> GetByYearAsync(Guid userId, int year, CancellationToken cancellationToken);
}
