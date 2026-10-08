using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Queries;

public class DailyDiaryQueries : IDailyDiaryQueries
{
    private readonly ApplicationDbContext _context;

    public DailyDiaryQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<DailyDiaryListItemDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => _context.DailyDiaries
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => new DailyDiaryListItemDto(d.Id, d.Date, d.Text))
            .ToListAsync(cancellationToken);

    public Task<DailyDiaryDetailsDto?> GetByIdAsync(Guid userId, Guid diaryId, CancellationToken cancellationToken)
        => _context.DailyDiaries
            .AsNoTracking()
            .Where(d => d.Id == diaryId && d.UserId == userId)
            .Select(d => new DailyDiaryDetailsDto(d.Id, d.Date, d.Text))
            .FirstOrDefaultAsync(cancellationToken);
}
