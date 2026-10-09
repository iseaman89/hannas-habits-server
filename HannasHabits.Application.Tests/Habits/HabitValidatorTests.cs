using FluentValidation.TestHelper;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;
using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Habits.Commands.UpdateHabit;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;

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
    public void TheSchedule_IsRequired_BecauseAnUpdateReplacesTheWholeHabit()
    {
        var command = Valid();

        command.Schedule = null!;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Schedule);

        command.Schedule = [];
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Schedule);

        command.Schedule = [(DayOfWeek)(-1)];
        _validator.TestValidate(command).ShouldHaveValidationErrorFor("Schedule[0]");
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
    public void AsOf_IsOptional()
    {
        _validator.TestValidate(new GetHabitsOverviewQuery(Day, Day, null)).ShouldNotHaveValidationErrorFor(x => x.AsOf);
    }
}
