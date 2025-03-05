using HannaHabitsService.Models;

namespace HannaHabitsService.Repositories.YearResolutions;

public interface IYearResolutionRepository : IGenericRepository<YearResolution>
{
    Task<IEnumerable<YearResolution>> GetYearResolutionsByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<YearResolution?> GetYearResolutionByIdAsync(int id, CancellationToken cancellationToken);
}