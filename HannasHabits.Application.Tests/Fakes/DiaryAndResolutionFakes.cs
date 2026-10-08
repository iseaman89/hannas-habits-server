using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;
using HannasHabits.Application.Resolutions;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Tests.Fakes;

/// <summary>Holds the diaries in a list; hands out an entry only to its owner.</summary>
internal sealed class FakeDailyDiaryRepository : IDailyDiaryRepository
{
    public List<DailyDiary> Entries { get; } = [];
    public List<DailyDiary> Removed { get; } = [];

    public Task<DailyDiary?> GetByDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
        => Task.FromResult(Entries.FirstOrDefault(d => d.UserId == userId && d.Date == date));

    public void Add(DailyDiary dailyDiary) => Entries.Add(dailyDiary);

    public void Remove(DailyDiary dailyDiary)
    {
        Entries.Remove(dailyDiary);
        Removed.Add(dailyDiary);
    }
}

/// <summary>Returns canned answers and remembers what it was asked.</summary>
internal sealed class FakeDailyDiaryQueries : IDailyDiaryQueries
{
    public DailyDiaryDto? ByDate { get; set; }
    public List<DailyDiaryDayDto> Days { get; set; } = [];

    public Guid? AskedForUser { get; private set; }
    public DateOnly? AskedForDate { get; private set; }
    public (DateOnly? From, DateOnly? To)? AskedForRange { get; private set; }

    public Task<DailyDiaryDto?> GetByDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        AskedForUser = userId;
        AskedForDate = date;
        return Task.FromResult(ByDate);
    }

    public Task<List<DailyDiaryDayDto>> GetDaysAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        AskedForUser = userId;
        AskedForRange = (from, to);
        return Task.FromResult(Days);
    }
}

/// <summary>Holds the resolutions in a list; hands them out only to their owner (and only for the right year).</summary>
internal sealed class FakeResolutionRepository : IResolutionRepository
{
    public List<Resolution> Resolutions { get; } = [];
    public List<Resolution> Removed { get; } = [];

    /// <summary>Overrides the answer of <see cref="CountForYearAsync"/> (e.g. "the year is full").</summary>
    public int? CountOverride { get; set; }

    public (Guid UserId, int Year)? CountAskedFor { get; private set; }

    public Task<Resolution?> GetByIdAsync(Guid userId, int year, Guid resolutionId, CancellationToken cancellationToken)
        => Task.FromResult(Resolutions.FirstOrDefault(r => r.Id == resolutionId && r.UserId == userId && r.Year == year));

    public Task<int> CountForYearAsync(Guid userId, int year, CancellationToken cancellationToken)
    {
        CountAskedFor = (userId, year);
        return Task.FromResult(CountOverride ?? Resolutions.Count(r => r.UserId == userId && r.Year == year));
    }

    public void Add(Resolution resolution) => Resolutions.Add(resolution);

    public void Remove(Resolution resolution)
    {
        Resolutions.Remove(resolution);
        Removed.Add(resolution);
    }
}

internal sealed class FakeResolutionQueries : IResolutionQueries
{
    public List<ResolutionDto> Result { get; set; } = [];

    public (Guid UserId, int Year)? AskedFor { get; private set; }

    public Task<List<ResolutionDto>> GetByYearAsync(Guid userId, int year, CancellationToken cancellationToken)
    {
        AskedFor = (userId, year);
        return Task.FromResult(Result);
    }
}
