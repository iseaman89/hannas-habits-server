using HannasHabits.Application.Resolutions;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Queries;

public class ResolutionQueries : IResolutionQueries
{
    private readonly ApplicationDbContext _context;

    public ResolutionQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ResolutionDto>> GetByYearAsync(Guid userId, int year, CancellationToken cancellationToken)
    {
        // A left join: a resolution without a habit stays in the result. The habit title is a value object, so the
        // query loads it as stored and the DTO is built in memory (like the diary queries). The user filter on the joined
        // habits is redundant with the check when linking, but data isolation should not depend on a rule elsewhere.
        var rows = await (
                from resolution in _context.Resolutions.AsNoTracking()
                where resolution.UserId == userId && resolution.Year == year
                join habit in _context.Habits.AsNoTracking().Where(h => h.UserId == userId)
                    on resolution.HabitId equals habit.Id into habits
                from habit in habits.DefaultIfEmpty()
                orderby resolution.CreatedAt, resolution.Id
                select new { resolution.Id, resolution.Title, resolution.Kept, resolution.HabitId, HabitTitle = habit.Title })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ResolutionDto(row.Id, row.Title, row.Kept, row.HabitId, row.HabitTitle?.Value))
            .ToList();
    }
}
