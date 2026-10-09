using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Entities;

public class HabitTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 10, 1);

    private static Habit NewHabit(string title = "Read", string? description = null, HabitSchedule? schedule = null)
        => Habit.Create(UserId, HabitTitle.Create(title), Start, description, schedule);

    // ---- Create ----

    [Fact]
    public void Create_SetsTheGivenValues()
    {
        var schedule = HabitSchedule.Create([DayOfWeek.Monday]);

        var habit = Habit.Create(UserId, HabitTitle.Create("Read"), Start, "20 pages", schedule);

        Assert.Equal(UserId, habit.UserId);
        Assert.Equal("Read", habit.Title.Value);
        Assert.Equal("20 pages", habit.Description);
        Assert.Equal(schedule, habit.Schedule);
        Assert.Equal(Start, habit.StartDate);
        Assert.Empty(habit.Records);
    }

    [Fact]
    public void Create_WithoutASchedule_PlansEveryDay()
    {
        Assert.Equal(HabitSchedule.Daily, NewHabit().Schedule);
    }

    [Fact]
    public void Create_GivesEveryHabitItsOwnId_AndAUtcCreationTime()
    {
        var first = NewHabit();
        var second = NewHabit();

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(DateTimeKind.Utc, first.CreatedAt.Kind);
        Assert.InRange(first.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Theory]
    [InlineData(2000, 1, 1)]
    [InlineData(2026, 10, 8)]
    [InlineData(2100, 12, 31)]
    public void Create_AcceptsAStartDateInsideTheRange(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        Assert.Equal(date, Habit.Create(UserId, HabitTitle.Create("Read"), date).StartDate);
    }

    [Theory]
    [InlineData(1999, 12, 31)]
    [InlineData(2101, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void Create_RejectsAStartDateOutsideTheRange(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        Assert.Throws<DomainException>(() => Habit.Create(UserId, HabitTitle.Create("Read"), date));
    }

    [Fact]
    public void TheStartDateRangeConstantsAreTheDocumentedOnes()
    {
        // The validator, the EF check constraint and the API docs are built from these.
        Assert.Equal(new DateOnly(2000, 1, 1), Habit.MinStartDate);
        Assert.Equal(new DateOnly(2100, 12, 31), Habit.MaxStartDate);
    }

    [Fact]
    public void AFutureStartDateIsFine()
    {
        var nextYear = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);

        Assert.Equal(nextYear, Habit.Create(UserId, HabitTitle.Create("Read"), nextYear).StartDate);
    }

    // ---- description ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankDescriptionMeansNoDescription(string? description)
    {
        Assert.Null(NewHabit(description: description).Description);
    }

    [Fact]
    public void TheDescriptionIsTrimmed()
    {
        Assert.Equal("20 pages", NewHabit(description: "  20 pages\n").Description);
    }

    [Fact]
    public void TheDescriptionMayBeAsLongAsTheLimit_ButNotLonger()
    {
        var longest = new string('a', Habit.DescriptionMaxLength);

        Assert.Equal(longest, NewHabit(description: longest).Description);
        Assert.Throws<DomainException>(() => NewHabit(description: longest + "a"));
    }

    // ---- Update ----

    [Fact]
    public void Update_ReplacesTitleDescriptionAndSchedule_ButNotTheStartDate()
    {
        var habit = NewHabit("Read", "old", HabitSchedule.Daily);
        var schedule = HabitSchedule.Create([DayOfWeek.Saturday, DayOfWeek.Sunday]);

        habit.Update(HabitTitle.Create("Run"), "new", schedule);

        Assert.Equal("Run", habit.Title.Value);
        Assert.Equal("new", habit.Description);
        Assert.Equal(schedule, habit.Schedule);
        Assert.Equal(Start, habit.StartDate);
    }

    [Fact]
    public void Update_WithoutADescription_RemovesIt()
    {
        var habit = NewHabit(description: "old");

        habit.Update(habit.Title, null, habit.Schedule);

        Assert.Null(habit.Description);
    }

    [Fact]
    public void ARejectedUpdate_ChangesNothing()
    {
        var habit = NewHabit("Read", "old", HabitSchedule.Daily);
        var tooLong = new string('a', Habit.DescriptionMaxLength + 1);

        Assert.Throws<DomainException>(() =>
            habit.Update(HabitTitle.Create("Run"), tooLong, HabitSchedule.Create([DayOfWeek.Monday])));

        Assert.Equal("Read", habit.Title.Value);
        Assert.Equal("old", habit.Description);
        Assert.Equal(HabitSchedule.Daily, habit.Schedule);
    }

    // ---- records ----

    [Fact]
    public void MarkCompleted_AddsARecordForThatDay()
    {
        var habit = NewHabit();
        var day = new DateOnly(2026, 10, 8);

        var record = habit.MarkCompleted(day);

        Assert.Equal(day, record.Date);
        Assert.Equal(habit.Id, record.HabitId);
        Assert.Same(record, Assert.Single(habit.Records));
        Assert.Same(record, habit.RecordOn(day));
    }

    [Fact]
    public void MarkCompleted_AllowsDifferentDays()
    {
        var habit = NewHabit();

        habit.MarkCompleted(new DateOnly(2026, 10, 7));
        habit.MarkCompleted(new DateOnly(2026, 10, 8));

        Assert.Equal(2, habit.Records.Count);
    }

    [Fact]
    public void MarkCompleted_RejectsADayThatIsAlreadyCompleted()
    {
        var habit = NewHabit();
        var day = new DateOnly(2026, 10, 8);
        habit.MarkCompleted(day);

        Assert.Throws<DomainException>(() => habit.MarkCompleted(day));
        Assert.Single(habit.Records);
    }

    [Fact]
    public void RecordOn_IsNullForADayThatWasNotCompleted()
    {
        var habit = NewHabit();
        habit.MarkCompleted(new DateOnly(2026, 10, 8));

        Assert.Null(habit.RecordOn(new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void UnmarkCompleted_RemovesTheRecord_AndTheDayCanBeMarkedAgain()
    {
        var habit = NewHabit();
        var day = new DateOnly(2026, 10, 8);
        habit.MarkCompleted(day);

        habit.UnmarkCompleted(day);

        Assert.Empty(habit.Records);
        Assert.Null(habit.RecordOn(day));
        Assert.Equal(day, habit.MarkCompleted(day).Date);
    }

    [Fact]
    public void UnmarkCompleted_OnlyRemovesThatDay()
    {
        var habit = NewHabit();
        habit.MarkCompleted(new DateOnly(2026, 10, 7));
        habit.MarkCompleted(new DateOnly(2026, 10, 8));

        habit.UnmarkCompleted(new DateOnly(2026, 10, 7));

        Assert.Equal(new DateOnly(2026, 10, 8), Assert.Single(habit.Records).Date);
    }

    [Fact]
    public void UnmarkCompleted_RejectsADayThatWasNotCompleted()
    {
        var habit = NewHabit();

        Assert.Throws<DomainException>(() => habit.UnmarkCompleted(new DateOnly(2026, 10, 8)));
    }
}
