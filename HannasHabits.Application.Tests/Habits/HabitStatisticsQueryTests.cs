using FluentValidation.TestHelper;
using HannasHabits.Application.Habits.Queries.GetHabitsStatistics;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace HannasHabits.Application.Tests.Habits;

public class GetHabitsStatisticsQueryHandlerTests
{
    private static readonly DateOnly Today = TestClock.Today; // Thursday, 8 October 2026

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _repository = new();
    private readonly FakeTimeProvider _clock = TestClock.Create();

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private Task<List<HabitStatisticsDto>> Handle(DateOnly? from, DateOnly? to, DateOnly? asOf = null)
        => new GetHabitsStatisticsQueryHandler(new FakeHabitQueries(_repository), _user, _clock)
            .Handle(new GetHabitsStatisticsQuery(from, to, asOf), CancellationToken.None);

    private static readonly HabitSchedule MondaysAndThursdays = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Thursday]);

    [Fact]
    public async Task ReturnsEveryHabitOfTheUser_InCreationOrder_AndNoOneElsesHabits()
    {
        var queries = new FakeHabitQueries(_repository);
        var first = Make.NewHabit(_user.UserId, "Read", D(1, 5));
        var second = Make.NewHabit(_user.UserId, "Run", D(2, 6), MondaysAndThursdays);
        _repository.Habits.Add(first);
        _repository.Habits.Add(second);
        _repository.Habits.Add(Make.NewHabit(Guid.NewGuid(), "Somebody else's"));

        var result = await new GetHabitsStatisticsQueryHandler(queries, _user, _clock)
            .Handle(new GetHabitsStatisticsQuery(D(10, 1), D(10, 31), null), CancellationToken.None);

        Assert.Collection(result,
            item =>
            {
                Assert.Equal(first.Id, item.Id);
                Assert.Equal("Read", item.Title);
                Assert.Equal(D(1, 5), item.StartDate);
                Assert.Equal(ScheduleKind.Weekly, item.Recurrence.Kind);
            },
            item =>
            {
                Assert.Equal(second.Id, item.Id);
                Assert.Equal([DayOfWeek.Monday, DayOfWeek.Thursday], item.Recurrence.Weekdays);
            });
        Assert.Equal([_user.UserId], queries.AskedForUsers);
    }

    [Fact]
    public async Task CountsThePlannedAndDoneDaysOfTheRange_AsOfTheGivenDay()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId, "Gym", D(1, 1), MondaysAndThursdays,
            completed: [D(10, 1), D(10, 5), D(10, 12)]));

        var result = await Handle(D(10, 1), D(10, 31), asOf: D(10, 14));

        // Due up to Wednesday 14: Thu 1, Mon 5, Thu 8, Mon 12.
        var gym = Assert.Single(result);
        Assert.Equal(CountingUnit.Days, gym.Unit);
        Assert.Equal(4, gym.Planned);
        Assert.Equal(3, gym.Done);
    }

    [Fact]
    public async Task WithoutAsOf_TheServersDateIsToday_AndAnOpenTodayIsNotAMiss()
    {
        // The fake clock says Thursday 8 October; that Thursday is due and still open.
        _repository.Habits.Add(Make.NewHabit(_user.UserId, "Gym", D(1, 1), MondaysAndThursdays,
            completed: [D(10, 1), D(10, 5)]));

        var gym = Assert.Single(await Handle(D(10, 1), D(10, 31)));

        Assert.Equal(2, gym.Planned);
        Assert.Equal(2, gym.Done);
    }

    [Fact]
    public async Task LastDoneOn_IsTheLatestDayUpToAsOf_FromTheWholeHistory_NotJustTheRange()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId, "Gym", D(1, 1), completed: [D(8, 3), D(10, 5), D(10, 20)]));

        // The range is September; the habit was last done in October (and once more after "today").
        var gym = Assert.Single(await Handle(D(9, 1), D(9, 30), asOf: D(10, 8)));

        Assert.Equal(D(10, 5), gym.LastDoneOn);
    }

    [Fact]
    public async Task AHabitThatWasNeverDone_HasNoLastDay()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId, "Gym", D(1, 1)));

        var gym = Assert.Single(await Handle(D(10, 1), D(10, 31)));

        Assert.Null(gym.LastDoneOn);
        Assert.Equal(0, gym.Done);
    }

    [Fact]
    public async Task AQuotaHabit_IsCountedInWeeks()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId, "Run", D(1, 1), HabitSchedule.TimesPerWeek(2),
            completed: [D(10, 6), D(10, 8), D(10, 13)]));

        // The weeks ending in October up to Thursday 15: 28 Sep-4 Oct (nothing), 5-11 (two - met); 12-18 is running with one.
        var run = Assert.Single(await Handle(D(10, 1), D(10, 31), asOf: D(10, 15)));

        Assert.Equal(CountingUnit.Weeks, run.Unit);
        Assert.Equal(2, run.Planned);
        Assert.Equal(1, run.Done);
        Assert.Equal(D(10, 13), run.LastDoneOn);
    }

    [Fact]
    public async Task SubHabits_AreCountedOnTheirOwnWeekdays_InTheOrderTheyWereAdded()
    {
        var habit = Make.NewHabit(_user.UserId, "Sport", D(1, 1), MondaysAndThursdays);
        var legs = habit.AddSubHabit(HabitTitle.Create("Legs"), WeekdaySet.Of([DayOfWeek.Monday]));
        var arms = habit.AddSubHabit(HabitTitle.Create("Arms"), WeekdaySet.Of([DayOfWeek.Thursday]));
        habit.MarkSubHabitCompleted(legs.Id, D(10, 5));
        habit.MarkSubHabitCompleted(legs.Id, D(10, 12));
        habit.MarkSubHabitCompleted(arms.Id, D(10, 8));
        _repository.Habits.Add(habit);

        var sport = Assert.Single(await Handle(D(10, 1), D(10, 31), asOf: D(10, 14)));

        Assert.Collection(sport.SubHabits,
            part =>
            {
                Assert.Equal(legs.Id, part.Id);
                Assert.Equal("Legs", part.Title);
                Assert.Equal([DayOfWeek.Monday], part.Weekdays);
                Assert.Equal(2, part.Planned); // Mon 5 and Mon 12
                Assert.Equal(2, part.Done);
                Assert.Equal(D(10, 12), part.LastDoneOn);
            },
            part =>
            {
                Assert.Equal(arms.Id, part.Id);
                Assert.Equal(2, part.Planned); // Thu 1 and Thu 8
                Assert.Equal(1, part.Done);
                Assert.Equal(D(10, 8), part.LastDoneOn);
            });
    }

    [Fact]
    public async Task AHabitWithoutParts_HasAnEmptyList()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId));

        Assert.Empty(Assert.Single(await Handle(D(10, 1), D(10, 31))).SubHabits);
    }
}

public class GetHabitsStatisticsQueryValidatorTests
{
    private readonly GetHabitsStatisticsQueryValidator _validator = new();
    private static readonly DateOnly Day = new(2026, 10, 8);

    [Fact]
    public void BothEndsOfTheRange_AreRequired()
    {
        _validator.TestValidate(new GetHabitsStatisticsQuery(null, Day, null)).ShouldHaveValidationErrorFor(x => x.From);
        _validator.TestValidate(new GetHabitsStatisticsQuery(Day, null, null)).ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void ARangeMayBeASingleDay_ButNotBackwards()
    {
        _validator.TestValidate(new GetHabitsStatisticsQuery(Day, Day, null)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetHabitsStatisticsQuery(Day, Day.AddDays(-1), null)).ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void TheRange_IsLimitedToALeapYear()
    {
        var longest = new GetHabitsStatisticsQuery(Day, Day.AddDays(GetHabitsStatisticsQueryValidator.MaxRangeDays - 1), null);
        var tooLong = new GetHabitsStatisticsQuery(Day, Day.AddDays(GetHabitsStatisticsQueryValidator.MaxRangeDays), null);

        Assert.Equal(366, GetHabitsStatisticsQueryValidator.MaxRangeDays);
        _validator.TestValidate(longest).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(x => x.To);
        _validator.TestValidate(new GetHabitsStatisticsQuery(DateOnly.MinValue, DateOnly.MaxValue, null))
            .ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void AsOf_IsOptional()
    {
        _validator.TestValidate(new GetHabitsStatisticsQuery(Day, Day, null)).ShouldNotHaveValidationErrorFor(x => x.AsOf);
    }
}
