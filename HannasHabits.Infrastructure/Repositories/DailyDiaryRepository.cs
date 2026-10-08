using HannasHabits.Application.DailyDiaries;
using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Repositories;

public class DailyDiaryRepository : IDailyDiaryRepository
{
    private readonly ApplicationDbContext _context;

    public DailyDiaryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<DailyDiary?> GetByIdAsync(Guid userId, Guid diaryId, CancellationToken cancellationToken)
        => _context.DailyDiaries
            .FirstOrDefaultAsync(d => d.Id == diaryId && d.UserId == userId, cancellationToken);

    public void Add(DailyDiary dailyDiary) => _context.DailyDiaries.Add(dailyDiary);

    public void Remove(DailyDiary dailyDiary) => _context.DailyDiaries.Remove(dailyDiary);
}
