using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>
/// Rows written with plain SQL, past the Domain: how the database looks to anything that does not go through the
/// application (a script, a migration, a bug) - which is exactly what constraints and foreign keys are there for.
/// </summary>
public static class Sql
{
    public static async Task<Guid> InsertUserAsync(ApplicationDbContext db, string? email = null)
    {
        var id = Guid.NewGuid();
        email ??= $"sql-{id:N}@example.com";

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
                "SecurityStamp", "ConcurrencyStamp", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            VALUES ({id}, {email}, {email.ToUpperInvariant()}, {email}, {email.ToUpperInvariant()}, false,
                {Guid.NewGuid().ToString()}, {Guid.NewGuid().ToString()}, false, false, true, 0)
            """);

        return id;
    }

    public static async Task<Guid> InsertHabitAsync(
        ApplicationDbContext db, Guid userId, string title = "Habit", int schedule = 127,
        string startDate = "2026-01-01", DateTime? createdAt = null)
    {
        var id = Guid.NewGuid();
        var start = DateOnly.Parse(startDate);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Habits" ("Id", "CreatedAt", "UserId", "Title", "Schedule", "StartDate")
            VALUES ({id}, {createdAt ?? DateTime.UtcNow}, {userId}, {title}, {schedule}, {start})
            """);

        return id;
    }

    public static async Task<Guid> InsertRecordAsync(ApplicationDbContext db, Guid habitId, string date)
    {
        var id = Guid.NewGuid();
        var day = DateOnly.Parse(date);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "HabitRecords" ("Id", "CreatedAt", "HabitId", "Date") VALUES ({id}, {DateTime.UtcNow}, {habitId}, {day})
            """);

        return id;
    }

    public static async Task<Guid> InsertDiaryAsync(
        ApplicationDbContext db, Guid userId, string date, int? mood = 3, int? body = null, int? mind = null)
    {
        var id = Guid.NewGuid();
        var day = DateOnly.Parse(date);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DailyDiary" ("Id", "CreatedAt", "UserId", "Date", "Mood", "Body", "Mind", "Grateful", "Learned", "Tasks")
            VALUES ({id}, {DateTime.UtcNow}, {userId}, {day}, {mood}, {body}, {mind}, ARRAY[]::text[], ARRAY[]::text[], '[]'::jsonb)
            """);

        return id;
    }

    public static async Task<Guid> InsertResolutionAsync(
        ApplicationDbContext db, Guid userId, int year = 2026, string title = "Resolution", Guid? habitId = null)
    {
        var id = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Resolutions" ("Id", "CreatedAt", "UserId", "Year", "Title", "Kept", "HabitId")
            VALUES ({id}, {DateTime.UtcNow}, {userId}, {year}, {title}, false, {habitId})
            """);

        return id;
    }

    public static async Task<Guid> InsertRefreshTokenAsync(ApplicationDbContext db, Guid userId, string? hash = null)
    {
        var id = Guid.NewGuid();
        hash ??= Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RefreshTokens" ("Id", "UserId", "TokenHash", "CreatedAt", "ExpiresAt")
            VALUES ({id}, {userId}, {hash}, {DateTime.UtcNow}, {DateTime.UtcNow.AddDays(30)})
            """);

        return id;
    }

    /// <summary>How many rows of the table have <paramref name="value"/> in <paramref name="column"/>.</summary>
    public static Task<long> CountAsync(ApplicationDbContext db, string table, string column, object value)
        => ScalarAsync(db, $"SELECT COUNT(*) FROM \"{table}\" WHERE \"{column}\" = @p0", value);

    /// <summary>A query that returns one number; the parameters are named <c>@p0</c>, <c>@p1</c>, ...</summary>
    public static async Task<long> ScalarAsync(ApplicationDbContext db, string sql, params object[] parameters)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();

        command.CommandText = sql;
        for (var i = 0; i < parameters.Length; i++)
            command.Parameters.Add(new NpgsqlParameter($"p{i}", parameters[i]));

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>Runs the statement and returns the SQLSTATE and constraint the database rejected it with.</summary>
    public static async Task<PostgresException> RejectedAsync(Func<Task> statement)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(statement);

        return exception switch
        {
            PostgresException postgres => postgres,
            DbUpdateException { InnerException: PostgresException postgres } => postgres,
            { InnerException: PostgresException postgres } => postgres,
            _ => throw new Xunit.Sdk.XunitException($"Expected the database to reject the statement, but got {exception.GetType().Name}: {exception.Message}")
        };
    }
}

/// <summary>SQLSTATEs of the rejections the tests expect.</summary>
public static class SqlState
{
    public const string UniqueViolation = "23505";
    public const string ForeignKeyViolation = "23503";
    public const string CheckViolation = "23514";
    public const string StringTooLong = "22001";
}
