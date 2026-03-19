using HannasHabits.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Habit> Habits { get; }
    DbSet<HabitRecord> HabitRecords { get; }
    DbSet<DailyDiary> DailyDiaries { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}