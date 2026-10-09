using System.Reflection;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HannasHabits.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IUnitOfWork
{
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitRecord> HabitRecords => Set<HabitRecord>();
    public DbSet<DailyDiary> DailyDiaries => Set<DailyDiary>();
    public DbSet<Resolution> Resolutions => Set<Resolution>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }

    // Explicit interface implementation on purpose: only the application's use cases get application-level exceptions.
    // ASP.NET Identity calls the context directly and relies on the raw EF exceptions (e.g. its concurrency handling).
    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // e.g. two requests delete the same row: the second one finds nothing to delete.
            throw new ConflictException("The resource was changed by another request. Please retry.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                                                  {
                                                      SqlState: PostgresErrorCodes.UniqueViolation
                                                  })
        {
            throw new DuplicateEntryException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                                                  {
                                                      SqlState: PostgresErrorCodes.ForeignKeyViolation
                                                  })
        {
            // e.g. the habit a new resolution links to was deleted by another request in the meantime.
            throw new ConflictException("A related resource no longer exists. Please retry.", exception);
        }
    }
}
