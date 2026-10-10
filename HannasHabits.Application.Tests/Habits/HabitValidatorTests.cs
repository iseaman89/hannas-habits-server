using FluentValidation.TestHelper;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;
using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Habits.Commands.UpdateHabit;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;
using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Domain.Enums;

namespace HannasHabits.Application.Tests.Habits;

public class CreateHabitCommandValidatorTests
{
    private readonly CreateHabitCommandValidator _validator = new();

    private static CreateHabitCommand Valid() => new() { Title = "Read" };

    [Fact]
    public void ACommandWithJustATitle_IsValid()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingOrBlankTitle_IsRejected(string? title)
    {
        var command = Valid();
        command.Title = title!;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TheTitle_MayHave150Characters_NotMore()
    {
        var command = Valid();

        command.Title = new string('a', 150);
        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.Title);

        command.Title = new string('a', 151);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TheDescription_IsOptional_AndMayHave500Characters()
    {
        var command = Valid();

        command.Description = null;
        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.Description);

        command.Description = new string('a', 500);
        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.Description);

        command.Description = new string('a', 501);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(2000, 1, 1, true)]
    [InlineData(2026, 10, 8, true)]
    [InlineData(2100, 12, 31, true)]
    [InlineData(1999, 12, 31, false)]
    [InlineData(2101, 1, 1, false)]
    [InlineData(1, 1, 1, false)]
    public void TheStartDate_MustBeInsideTheRange(int year, int month, int day, bool valid)
    {
        var command = Valid();
        command.StartDate = new DateOnly(year, month, day);

        var result = _validator.TestValidate(command);

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.StartDate);
        else
            result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public void WithoutAStartDate_TheServerFillsItIn()
    {
        var command = Valid();
        command.StartDate = null;

        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public void NoScheduleMeansEveryDay_ButAnExplicitlyEmptyOneIsAMistake()
    {
        var command = Valid();

        command.Schedule = null;
        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.Schedule);

        command.Schedule = [];
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Schedule);
    }

    [Fact]
    public void AScheduleWithANumberThatIsNoDay_IsRejected_AtThatPosition()
    {
        var command = Valid();
        command.Schedule = [DayOfWeek.Monday, (DayOfWeek)7];

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Schedule[1]");
        result.ShouldNotHaveValidationErrorFor("Schedule[0]");
    }
}

public class UpdateHabitCommandValidatorTests
{
    private readonly UpdateHabitCommandValidator _validator = new();

    private static UpdateHabitCommand Valid() => new()
    {
        Id = Guid.NewGuid(), Title = "Read", Schedule = [DayOfWeek.Monday]
    };

    [Fact]
    public void AValidCommand_Passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AnEmptyId_IsRejected()
    {
        var command = Valid();
        command.Id = Guid.Empty;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankTitle_IsRejected(string title)
    {
        var command = Valid();
        command.Title = title;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TheTitleAndDescription_HaveTheSameLimitsAsOnCreate()
    {
        var command = Valid();

        command.Title = new string('a', 151);
        command.Description = new string('a', 501);

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void TheStartDate_IsOptional_AndLimitedToTheSameRangeAsOnCreate()
    {
        var command = Valid();

        command.StartDate = null;
        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.StartDate);

        command.StartDate = new DateOnly(2000, 1, 1);
        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.StartDate);

        command.StartDate = new DateOnly(1999, 12, 31);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.StartDate);

        command.StartDate = new DateOnly(2101, 1, 1);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public void TheRule_IsRequired_BecauseAnUpdateReplacesTheWholeHabit_ByRecurrenceOrByTheDeprecatedSchedule()
    {
        var command = Valid();

        command.Schedule = null;
        command.Recurrence = null;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Recurrence);

        command.Schedule = [];
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Schedule);

        command.Schedule = [(DayOfWeek)(-1)];
        _validator.TestValidate(command).ShouldHaveValidationErrorFor("Schedule[0]");

        command.Schedule = null;
        command.Recurrence = new RecurrenceInput { Kind = ScheduleKind.MonthlyOnDate, DayOfMonth = 15 };
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ARecurrence_IsCheckedWhereverItIsSent_AndTheDeprecatedListThenIsNotLookedAt()
    {
        var command = Valid();
        command.Schedule = [];
        command.Recurrence = new RecurrenceInput { Kind = ScheduleKind.EveryNDays, Interval = 1 };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Recurrence.Interval");
        result.ShouldNotHaveValidationErrorFor(x => x.Schedule);
    }
}

public class RecurrenceInputValidatorTests
{
    private readonly RecurrenceInputValidator _validator = new();

    private static readonly DateOnly Anchor = new(2026, 10, 7);

    private static RecurrenceInput Weekly(int? interval = null, DateOnly? anchor = null, params DayOfWeek[] days)
        => new() { Kind = ScheduleKind.Weekly, Weekdays = days.Length == 0 ? [DayOfWeek.Monday] : days.ToList(), Interval = interval, Anchor = anchor };

    [Fact]
    public void AValidRuleOfEveryKind_Passes()
    {
        _validator.TestValidate(Weekly()).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(Weekly(2, Anchor)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(Weekly(2)).ShouldNotHaveAnyValidationErrors(); // the anchor is filled in
        _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.EveryNDays, Interval = 3 }).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.MonthlyOnWeekday, Occurrence = WeekdayOccurrence.Last, Weekday = DayOfWeek.Saturday })
            .ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.MonthlyOnDate, DayOfMonth = 31 }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void TheKind_IsRequired_AndMustBeAKnownOne()
    {
        _validator.TestValidate(new RecurrenceInput()).ShouldHaveValidationErrorFor(x => x.Kind);
        _validator.TestValidate(new RecurrenceInput { Kind = (ScheduleKind)9 }).ShouldHaveValidationErrorFor(x => x.Kind);
    }

    [Fact]
    public void Weekly_NeedsDays_EachOfThemADayOfTheWeek()
    {
        var none = new RecurrenceInput { Kind = ScheduleKind.Weekly };
        var empty = new RecurrenceInput { Kind = ScheduleKind.Weekly, Weekdays = [] };
        var wrong = new RecurrenceInput { Kind = ScheduleKind.Weekly, Weekdays = [DayOfWeek.Monday, (DayOfWeek)7] };

        _validator.TestValidate(none).ShouldHaveValidationErrorFor(x => x.Weekdays);
        _validator.TestValidate(empty).ShouldHaveValidationErrorFor(x => x.Weekdays);
        _validator.TestValidate(wrong).ShouldHaveValidationErrorFor("Weekdays[1]");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(52, true)]
    [InlineData(0, false)]
    [InlineData(53, false)]
    [InlineData(-1, false)]
    public void Weekly_TheIntervalIsInWeeks_UpToAYear(int interval, bool valid)
    {
        var result = _validator.TestValidate(Weekly(interval, Anchor));

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.Interval);
        else
            result.ShouldHaveValidationErrorFor(x => x.Interval);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(365, true)]
    [InlineData(1, false)]  // every day is the daily schedule
    [InlineData(366, false)]
    public void EveryNDays_TheIntervalIsRequired_AndBetween2And365(int interval, bool valid)
    {
        var result = _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.EveryNDays, Interval = interval });

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.Interval);
        else
            result.ShouldHaveValidationErrorFor(x => x.Interval);

        _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.EveryNDays }).ShouldHaveValidationErrorFor(x => x.Interval);
    }

    [Fact]
    public void MonthlyOnAWeekday_NeedsTheOccurrenceAndTheWeekday()
    {
        var onlyWeekday = new RecurrenceInput { Kind = ScheduleKind.MonthlyOnWeekday, Weekday = DayOfWeek.Monday };
        var onlyOccurrence = new RecurrenceInput { Kind = ScheduleKind.MonthlyOnWeekday, Occurrence = WeekdayOccurrence.First };
        var wrong = new RecurrenceInput { Kind = ScheduleKind.MonthlyOnWeekday, Occurrence = (WeekdayOccurrence)6, Weekday = (DayOfWeek)7 };

        _validator.TestValidate(onlyWeekday).ShouldHaveValidationErrorFor(x => x.Occurrence);
        _validator.TestValidate(onlyOccurrence).ShouldHaveValidationErrorFor(x => x.Weekday);
        _validator.TestValidate(wrong).ShouldHaveValidationErrorFor(x => x.Occurrence);
        _validator.TestValidate(wrong).ShouldHaveValidationErrorFor(x => x.Weekday);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(31, true)]
    [InlineData(0, false)]
    [InlineData(32, false)]
    public void MonthlyOnADate_TheDayOfTheMonthIsRequired_AndBetween1And31(int day, bool valid)
    {
        var result = _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.MonthlyOnDate, DayOfMonth = day });

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.DayOfMonth);
        else
            result.ShouldHaveValidationErrorFor(x => x.DayOfMonth);

        _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.MonthlyOnDate }).ShouldHaveValidationErrorFor(x => x.DayOfMonth);
    }

    [Theory]
    [InlineData(2000, 1, 1, true)]
    [InlineData(2100, 12, 31, true)]
    [InlineData(1999, 12, 31, false)]
    [InlineData(2101, 1, 1, false)]
    public void TheAnchor_IsOptional_ButInsideThePlausibleRange(int year, int month, int day, bool valid)
    {
        var result = _validator.TestValidate(new RecurrenceInput { Kind = ScheduleKind.EveryNDays, Interval = 3, Anchor = new DateOnly(year, month, day) });

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.Anchor);
        else
            result.ShouldHaveValidationErrorFor(x => x.Anchor);
    }

    [Fact]
    public void AFactThatDoesNotBelongToTheKind_IsNotChecked()
    {
        var input = new RecurrenceInput
        {
            Kind = ScheduleKind.MonthlyOnDate, DayOfMonth = 15, Interval = 9999, Weekdays = [], Occurrence = (WeekdayOccurrence)99
        };

        _validator.TestValidate(input).ShouldNotHaveAnyValidationErrors();
    }
}

public class MarkCompletedCommandValidatorTests
{
    [Fact]
    public void AnEmptyHabitId_IsRejected()
    {
        var validator = new MarkCompletedCommandValidator();

        validator.TestValidate(new MarkCompletedCommand(Guid.Empty, new DateOnly(2026, 10, 8))).ShouldHaveValidationErrorFor(x => x.HabitId);
        validator.TestValidate(new MarkCompletedCommand(Guid.NewGuid(), new DateOnly(2026, 10, 8))).ShouldNotHaveAnyValidationErrors();
    }
}

public class GetRecordsByHabitQueryValidatorTests
{
    private readonly GetRecordsByHabitQueryValidator _validator = new();
    private static readonly DateOnly Day = new(2026, 10, 8);

    [Fact]
    public void WithoutARange_TheQueryIsValid()
    {
        _validator.TestValidate(new GetRecordsByHabitQuery(Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AnEmptyHabitId_IsRejected()
    {
        _validator.TestValidate(new GetRecordsByHabitQuery(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.HabitId);
    }

    [Fact]
    public void OneEndOfTheRangeAlone_IsFine()
    {
        _validator.TestValidate(new GetRecordsByHabitQuery(Guid.NewGuid(), From: Day)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetRecordsByHabitQuery(Guid.NewGuid(), To: Day)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ARangeMayBeASingleDay_ButNotBackwards()
    {
        _validator.TestValidate(new GetRecordsByHabitQuery(Guid.NewGuid(), Day, Day)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetRecordsByHabitQuery(Guid.NewGuid(), Day, Day.AddDays(-1))).ShouldHaveValidationErrorFor(x => x.To);
    }
}

public class GetHabitsOverviewQueryValidatorTests
{
    private readonly GetHabitsOverviewQueryValidator _validator = new();
    private static readonly DateOnly Day = new(2026, 10, 8);

    [Fact]
    public void BothEndsOfTheRange_AreRequired()
    {
        _validator.TestValidate(new GetHabitsOverviewQuery(null, Day, null)).ShouldHaveValidationErrorFor(x => x.From);
        _validator.TestValidate(new GetHabitsOverviewQuery(Day, null, null)).ShouldHaveValidationErrorFor(x => x.To);
        _validator.TestValidate(new GetHabitsOverviewQuery(null, null, null)).ShouldHaveValidationErrorFor(x => x.From);
    }

    [Fact]
    public void ARangeMayBeASingleDay_ButNotBackwards()
    {
        _validator.TestValidate(new GetHabitsOverviewQuery(Day, Day, null)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetHabitsOverviewQuery(Day, Day.AddDays(5), Day)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetHabitsOverviewQuery(Day, Day.AddDays(-1), null)).ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void TheRange_IsLimitedToALeapYear_BecauseEveryDueDayOfItIsListed()
    {
        var longest = new GetHabitsOverviewQuery(Day, Day.AddDays(GetHabitsOverviewQueryValidator.MaxRangeDays - 1), null);
        var tooLong = new GetHabitsOverviewQuery(Day, Day.AddDays(GetHabitsOverviewQueryValidator.MaxRangeDays), null);

        Assert.Equal(366, GetHabitsOverviewQueryValidator.MaxRangeDays);
        _validator.TestValidate(longest).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(x => x.To);
        _validator.TestValidate(new GetHabitsOverviewQuery(DateOnly.MinValue, DateOnly.MaxValue, null)).ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void AsOf_IsOptional()
    {
        _validator.TestValidate(new GetHabitsOverviewQuery(Day, Day, null)).ShouldNotHaveValidationErrorFor(x => x.AsOf);
    }
}
