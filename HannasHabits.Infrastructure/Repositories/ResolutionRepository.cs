using HannasHabits.Application.Resolutions;
using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Repositories;

public class ResolutionRepository : IResolutionRepository
{
    private readonly ApplicationDbContext _context;

    public ResolutionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Resolution?> GetByIdAsync(Guid userId, int year, Guid resolutionId, CancellationToken cancellationToken)
        => _context.Resolutions
            .FirstOrDefaultAsync(r => r.Id == resolutionId && r.UserId == userId && r.Year == year, cancellationToken);

    public Task<int> CountForYearAsync(Guid userId, int year, CancellationToken cancellationToken)
        => _context.Resolutions
            .CountAsync(r => r.UserId == userId && r.Year == year, cancellationToken);

    public void Add(Resolution resolution) => _context.Resolutions.Add(resolution);

    public void Remove(Resolution resolution) => _context.Resolutions.Remove(resolution);
}
