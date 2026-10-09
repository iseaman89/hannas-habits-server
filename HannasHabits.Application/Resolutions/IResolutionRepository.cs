using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Resolutions;

/// <summary>
/// Command side of the <see cref="Resolution"/> aggregate: returns tracked entities to change them. Lookups take the
/// owner's id, so a resolution of another user is simply "not found". Persisting is done by <c>IUnitOfWork</c>.
/// </summary>
public interface IResolutionRepository
{
    /// <summary>The user's resolution with that id in that year, or <c>null</c> (also if it exists in another year).</summary>
    Task<Resolution?> GetByIdAsync(Guid userId, int year, Guid resolutionId, CancellationToken cancellationToken);

    /// <summary>How many resolutions the user has in that year.</summary>
    Task<int> CountForYearAsync(Guid userId, int year, CancellationToken cancellationToken);

    void Add(Resolution resolution);

    void Remove(Resolution resolution);
}
