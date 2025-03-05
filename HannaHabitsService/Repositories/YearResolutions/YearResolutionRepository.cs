using HannaHabitsService.Data;
using HannaHabitsService.Models;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Repositories.YearResolutions;

public class YearResolutionRepository(HannaDbContext context)
    : GenericRepository<YearResolution>(context), IYearResolutionRepository
{
    private readonly HannaDbContext _context = context;

    public async Task<IEnumerable<YearResolution>> GetYearResolutionsByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.YearResolutions.Where(y => y.UserId == userId)
            .Include(y => y.Resolutions)
            .ToListAsync(cancellationToken);
    }

    public async Task<YearResolution?> GetYearResolutionByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.YearResolutions.Where(y => y.Id == id)
            .Include(y => y.Resolutions)
            .FirstOrDefaultAsync(cancellationToken);
    }
}