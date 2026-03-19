using System.Reflection;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitRecord> HabitRecords => Set<HabitRecord>();
    public DbSet<DailyDiary> DailyDiaries => Set<DailyDiary>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }
}

// public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
// {
//     public DbSet<Habit> Habits { get; set; }
//     public DbSet<Completion> Completions { get; set; }
//     public DbSet<DailyDiary> DailyDiaries { get; set; }
//     public DbSet<DailyTask> DailyTasks { get; set; }
//     public DbSet<YearResolution> YearResolutions { get; set; }
//     public DbSet<Resolution> HabitRecords { get; set; }
//     
//
//     protected override void OnModelCreating(ModelBuilder modelBuilder)
//     {
//         modelBuilder.Entity<Habit>()
//             .HasMany(h => h.Completions)
//             .WithOne(c => c.Habit)
//             .HasForeignKey(c => c.HabitId)
//             .OnDelete(DeleteBehavior.Cascade);
//         
//         modelBuilder.Entity<DailyDiary>()
//             .HasMany(dd => dd.DailyTasks)
//             .WithOne(dt => dt.DailyDiary)
//             .HasForeignKey(dt => dt.DailyDiaryId)
//             .OnDelete(DeleteBehavior.Cascade);
//
//         modelBuilder.Entity<YearResolution>()
//             .HasMany(y => y.HabitRecords)
//             .WithOne(r => r.YearResolutions)
//             .HasForeignKey(r => r.YearResolutionsId)
//             .OnDelete(DeleteBehavior.Cascade);
//     }
// }