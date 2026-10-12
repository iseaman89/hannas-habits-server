using HannasHabits.Application.Tags;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Repositories;

public class TagRepository : ITagRepository
{
    private readonly ApplicationDbContext _context;

    public TagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Tag?> GetByIdAsync(Guid userId, Guid tagId, CancellationToken cancellationToken)
        => _context.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId, cancellationToken);

    public Task<Tag?> FindByNameAsync(Guid userId, TagName name, CancellationToken cancellationToken)
    {
        // A local: EF Core translates a captured string, not a property access on the value object.
        var key = name.Key;

        return _context.Tags
            .FirstOrDefaultAsync(t => t.UserId == userId && t.NameKey == key, cancellationToken);
    }

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken)
        => _context.Tags
            .CountAsync(t => t.UserId == userId, cancellationToken);

    public void Add(Tag tag) => _context.Tags.Add(tag);

    // For a tag that was only added (its insert failed), EF Core just stops tracking it.
    public void Remove(Tag tag) => _context.Tags.Remove(tag);
}
