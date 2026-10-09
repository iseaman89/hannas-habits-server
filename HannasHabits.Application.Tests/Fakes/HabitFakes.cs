using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.Habits;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Tests.Fakes;

/// <summary>
/// Holds the habits in a list. Like the real repository it hands out a habit only to its owner; unlike EF Core it stores
/// an added habit at once (not at the save), which is all these tests need.
/// </summary>
internal sealed class FakeHabitRepository : IHabitRepository
{
    public List<Habit> Habits { get; } = [];
    public List<Habit> Removed { get; } = [];

    public Task<Habit?> GetByIdAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => Task.FromResult(Habits.FirstOrDefault(h => h.Id == habitId && h.UserId == userId));

    public Task<Habit?> GetByIdWithRecordsAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
        => GetByIdAsync(userId, habitId, cancellationToken);

    public void Add(Habit habit) => Habits.Add(habit);

    public void Remove(Habit habit)
    {
        Habits.Remove(habit);
        Removed.Add(habit);
    }
}

/// <summary>The read side over the same list of habits; remembers which user it was asked for.</summary>
internal sealed class FakeHabitQueries : IHabitQueries
{
    private readonly FakeHabitRepository _repository;

    public FakeHabitQueries(FakeHabitRepository repository)
    {
        _repository = repository;
    }

    public List<Guid> AskedForUsers { get; } = [];

    /// <summary>Replaces the answer of <see cref="GetRecordsAsync"/> (e.g. to play the winner of a race).</summary>
    public Func<List<HabitRecordDto>?>? RecordsOverride { get; set; }

    public (DateOnly? From, DateOnly? To)? LastRecordsRange { get; private set; }

    public Task<List<HabitListItemDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        AskedForUsers.Add(userId);
        return Task.FromResult(_repository.Habits
            .Where(h => h.UserId == userId)
            .Select(h => new HabitListItemDto(h.Id, h.Title.Value, h.Description, h.Schedule.Days))
            .ToList());
    }

    public Task<List<HabitWithCompletedDates>> GetWithCompletedDatesAsync(Guid userId, CancellationToken cancellationToken)
    {
        AskedForUsers.Add(userId);
        return Task.FromResult(_repository.Habits
            .Where(h => h.UserId == userId)
            .Select(h => new HabitWithCompletedDates(
                h.Id, h.Title.Value, h.Schedule, h.StartDate, h.Records.Select(r => r.Date).ToList()))
            .ToList());
    }

    public Task<HabitDetailsDto?> GetByIdAsync(Guid userId, Guid habitId, CancellationToken cancellationToken)
    {
        AskedForUsers.Add(userId);
        var habit = _repository.Habits.FirstOrDefault(h => h.Id == habitId && h.UserId == userId);

        return Task.FromResult(habit is null
            ? null
            : new HabitDetailsDto(habit.Id, habit.Title.Value, habit.Description, habit.Schedule.Days, habit.StartDate, habit.CreatedAt));
    }

    public Task<List<HabitRecordDto>?> GetRecordsAsync(
        Guid userId, Guid habitId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        AskedForUsers.Add(userId);
        LastRecordsRange = (from, to);

        if (RecordsOverride is not null)
            return Task.FromResult(RecordsOverride());

        var habit = _repository.Habits.FirstOrDefault(h => h.Id == habitId && h.UserId == userId);

        return Task.FromResult(habit?.Records
            .Where(r => (from is null || r.Date >= from) && (to is null || r.Date <= to))
            .OrderBy(r => r.Date)
            .Select(r => new HabitRecordDto(r.Id, r.HabitId, r.Date))
            .ToList());
    }
}
