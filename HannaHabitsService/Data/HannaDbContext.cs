using HannaHabitsService.Models;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Data;

public class HannaDbContext(DbContextOptions<HannaDbContext> options) : DbContext(options)
{
    public DbSet<Habit> Habits { get; set; }
    public DbSet<Completion> Completions { get; set; }
    public DbSet<DailyDiary> DailyDiaries { get; set; }
    public DbSet<DailyTask> DailyTasks { get; set; }
    public DbSet<YearResolution> YearResolutions { get; set; }
    public DbSet<Resolution> Resolutions { get; set; }
    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Habit>()
            .HasMany(h => h.Completions)
            .WithOne(c => c.Habit)
            .HasForeignKey(c => c.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<DailyDiary>()
            .HasMany(dd => dd.DailyTasks)
            .WithOne(dt => dt.DailyDiary)
            .HasForeignKey(dt => dt.DailyDiaryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<YearResolution>()
            .HasMany(y => y.Resolutions)
            .WithOne(r => r.YearResolutions)
            .HasForeignKey(r => r.YearResolutionsId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}