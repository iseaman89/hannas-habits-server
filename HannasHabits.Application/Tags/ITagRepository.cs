using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tags;

/// <summary>
/// Command side of the <see cref="Tag"/> aggregate: returns tracked entities to change them. Every lookup takes the
/// owner's id, so a tag of another user is simply "not found". Persisting is done by <c>IUnitOfWork</c>.
/// </summary>
public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid userId, Guid tagId, CancellationToken cancellationToken);

    /// <summary>The user's tag with that name, ignoring case (<see cref="TagName.Key"/>), or <c>null</c>.</summary>
    Task<Tag?> FindByNameAsync(Guid userId, TagName name, CancellationToken cancellationToken);

    /// <summary>How many tags the user has.</summary>
    Task<int> CountAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Tag tag);

    /// <summary>Removes a stored tag; for one that was only added (and not saved) it just forgets it again.</summary>
    void Remove(Tag tag);
}
