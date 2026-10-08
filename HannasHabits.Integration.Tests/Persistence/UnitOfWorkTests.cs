using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Infrastructure.Repositories;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HannasHabits.Integration.Tests.Persistence;

// The unit of work turns what the database says ("duplicate key", "foreign key", "0 rows changed") into the vocabulary of
// the application (409), and only for the application's use cases - ASP.NET Identity saves through the same context and
// relies on the raw EF exceptions.
[Collection(IntegrationCollection.Name)]
public class UnitOfWorkTests : IAsyncLifetime
{
    private readonly TestEnvironment _environment;
    private ApplicationDbContext _db = null!;
    private Guid _user;

    public UnitOfWorkTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public async Task InitializeAsync()
    {
        _db = _environment.CreateContext();
        _user = await Sql.InsertUserAsync(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task<(Guid Habit, DateOnly Day)> HabitWithARecord()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        await Sql.InsertRecordAsync(_db, habit, "2026-10-08");
        return (habit, new DateOnly(2026, 10, 8));
    }

    // A second record for a day that already has one: the habit is loaded WITHOUT its records, so the Domain cannot see the duplicate.
    private async Task MarkWithoutSeeingTheExistingRecord(ApplicationDbContext context, Guid habit, DateOnly day)
        => (await new HabitRepository(context).GetByIdAsync(_user, habit, default))!.MarkCompleted(day);

    [Fact]
    public async Task ADuplicateKey_BecomesADuplicateEntryException_ThatIsAConflict()
    {
        var (habit, day) = await HabitWithARecord();
        await MarkWithoutSeeingTheExistingRecord(_db, habit, day);

        var exception = await Assert.ThrowsAsync<DuplicateEntryException>(() => ((IUnitOfWork)_db).SaveChangesAsync());

        Assert.IsAssignableFrom<ConflictException>(exception); // a 409 unless a handler knows better
        var postgres = Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(exception.InnerException).InnerException);
        Assert.Equal(SqlState.UniqueViolation, postgres.SqlState);
    }

    [Fact]
    public async Task AForeignKeyViolation_BecomesAConflict_ARelatedResourceIsGone()
    {
        _db.Resolutions.Add(Resolution.Create(_user, 2026, "Read", habitId: Guid.NewGuid(), resolutionsInYear: 0)); // no such habit

        var exception = await Assert.ThrowsAsync<ConflictException>(() => ((IUnitOfWork)_db).SaveChangesAsync());

        Assert.IsNotType<DuplicateEntryException>(exception);
        var postgres = Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(exception.InnerException).InnerException);
        Assert.Equal(SqlState.ForeignKeyViolation, postgres.SqlState);
    }

    [Fact]
    public async Task ARowThatWasDeletedMeanwhile_BecomesAConflict_ChangedByAnotherRequest()
    {
        await Sql.InsertDiaryAsync(_db, _user, "2026-10-08");
        await using var first = _environment.CreateContext();
        await using var second = _environment.CreateContext();
        var a = (await new DailyDiaryRepository(first).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;
        var b = (await new DailyDiaryRepository(second).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;
        first.Remove(a);
        second.Remove(b);
        await ((IUnitOfWork)first).SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => ((IUnitOfWork)second).SaveChangesAsync());

        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);
    }

    [Fact]
    public async Task ARowThatWasChangedMeanwhile_IsJustOverwritten_TheLastWriteWins()
    {
        // Habits and diaries carry no row version: two edits do not collide, the later one wins (by design, see the diary).
        await Sql.InsertDiaryAsync(_db, _user, "2026-10-08", mood: 3);
        await using var first = _environment.CreateContext();
        await using var second = _environment.CreateContext();
        var a = (await new DailyDiaryRepository(first).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;
        var b = (await new DailyDiaryRepository(second).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;
        a.Replace(DiaryContent.Create(Mood.Good, null, null, null, null, null, null));
        b.Replace(DiaryContent.Create(Mood.Bad, null, null, null, null, null, null));

        await ((IUnitOfWork)first).SaveChangesAsync();
        await ((IUnitOfWork)second).SaveChangesAsync();

        await using var check = _environment.CreateContext();
        Assert.Equal(Mood.Bad, (await new DailyDiaryRepository(check).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!.Mood);
    }

    [Fact]
    public async Task TheRawSaveOfTheContext_KeepsTheRawExceptions_ForAspNetIdentity()
    {
        var (habit, day) = await HabitWithARecord();
        await MarkWithoutSeeingTheExistingRecord(_db, habit, day);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());

        Assert.IsNotAssignableFrom<ConflictException>(exception);
    }

    [Fact]
    public async Task ASuccessfulSave_ReturnsTheNumberOfRows_NothingToSaveReturnsZero()
    {
        var uow = (IUnitOfWork)_db;
        Assert.Equal(0, await uow.SaveChangesAsync());

        _db.Habits.Add(Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1)));

        Assert.Equal(1, await uow.SaveChangesAsync());
    }

    [Fact]
    public async Task ADeletedUser_MakesLaterWritesOfTheirRequestsAConflict_NotAServerError()
    {
        var gone = await Sql.InsertUserAsync(_db);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "AspNetUsers" WHERE "Id" = {gone}""");
        _db.Habits.Add(Habit.Create(gone, HabitTitle.Create("Orphan"), new DateOnly(2026, 3, 1)));

        await Assert.ThrowsAsync<ConflictException>(() => ((IUnitOfWork)_db).SaveChangesAsync());
    }
}
