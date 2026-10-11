using FluentValidation.TestHelper;
using HannasHabits.Application.SubHabits.Commands.AddSubHabit;
using HannasHabits.Application.SubHabits.Commands.MarkSubHabitCompleted;
using HannasHabits.Application.SubHabits.Commands.RemoveSubHabit;
using HannasHabits.Application.SubHabits.Commands.UnmarkSubHabitCompleted;
using HannasHabits.Application.SubHabits.Commands.UpdateSubHabit;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tests.SubHabits;

public class AddSubHabitCommandValidatorTests
{
    private readonly AddSubHabitCommandValidator _validator = new();

    private static AddSubHabitCommand Valid(
        string title = "Legs", List<DayOfWeek>? weekdays = null, Guid? habitId = null)
        => new(habitId ?? Guid.NewGuid(), title, weekdays ?? [DayOfWeek.Monday]);

    [Fact]
    public void AValidCommand_Passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AnEmptyHabitId_IsRejected()
    {
        _validator.TestValidate(Valid(habitId: Guid.Empty)).ShouldHaveValidationErrorFor(x => x.HabitId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingOrBlankTitle_IsRejected(string? title)
    {
        _validator.TestValidate(Valid(title: title!)).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TheTitle_MayBeAsLongAsAHabitTitle_NotLonger()
    {
        _validator.TestValidate(Valid(new string('a', HabitTitle.MaxLength))).ShouldNotHaveValidationErrorFor(x => x.Title);
        _validator.TestValidate(Valid(new string('a', HabitTitle.MaxLength + 1))).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void MissingOrEmptyWeekdays_AreRejected()
    {
        _validator.TestValidate(new AddSubHabitCommand(Guid.NewGuid(), "Legs", null)).ShouldHaveValidationErrorFor(x => x.Weekdays);
        _validator.TestValidate(Valid(weekdays: [])).ShouldHaveValidationErrorFor(x => x.Weekdays);
    }

    [Fact]
    public void ADayThatIsNotAWeekday_IsRejected_WithItsPosition()
    {
        var result = _validator.TestValidate(Valid(weekdays: [DayOfWeek.Monday, (DayOfWeek)7]));

        result.ShouldHaveValidationErrorFor("Weekdays[1]");
    }
}

public class UpdateSubHabitCommandValidatorTests
{
    private readonly UpdateSubHabitCommandValidator _validator = new();

    [Fact]
    public void AValidCommand_Passes()
    {
        _validator
            .TestValidate(new UpdateSubHabitCommand(Guid.NewGuid(), Guid.NewGuid(), "Legs", [DayOfWeek.Friday]))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyIds_AreRejected()
    {
        var result = _validator.TestValidate(new UpdateSubHabitCommand(Guid.Empty, Guid.Empty, "Legs", [DayOfWeek.Friday]));

        result.ShouldHaveValidationErrorFor(x => x.HabitId);
        result.ShouldHaveValidationErrorFor(x => x.SubHabitId);
    }

    [Fact]
    public void ABlankTitle_AndNoWeekdays_AreRejected()
    {
        var result = _validator.TestValidate(new UpdateSubHabitCommand(Guid.NewGuid(), Guid.NewGuid(), " ", []));

        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.Weekdays);
    }
}

public class RemoveSubHabitCommandValidatorTests
{
    private readonly RemoveSubHabitCommandValidator _validator = new();

    [Fact]
    public void EmptyIds_AreRejected()
    {
        var result = _validator.TestValidate(new RemoveSubHabitCommand(Guid.Empty, Guid.Empty));

        result.ShouldHaveValidationErrorFor(x => x.HabitId);
        result.ShouldHaveValidationErrorFor(x => x.SubHabitId);
    }

    [Fact]
    public void AValidCommand_Passes()
    {
        _validator.TestValidate(new RemoveSubHabitCommand(Guid.NewGuid(), Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
    }
}

public class MarkSubHabitCompletedCommandValidatorTests
{
    private readonly MarkSubHabitCompletedCommandValidator _validator = new();
    private readonly UnmarkSubHabitCompletedCommandValidator _unmarkValidator = new();

    [Fact]
    public void AValidCommand_Passes()
    {
        var date = new DateOnly(2026, 10, 5);

        _validator.TestValidate(new MarkSubHabitCompletedCommand(Guid.NewGuid(), Guid.NewGuid(), date))
            .ShouldNotHaveAnyValidationErrors();
        _unmarkValidator.TestValidate(new UnmarkSubHabitCompletedCommand(Guid.NewGuid(), Guid.NewGuid(), date))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyIds_AreRejected()
    {
        var date = new DateOnly(2026, 10, 5);

        var result = _validator.TestValidate(new MarkSubHabitCompletedCommand(Guid.Empty, Guid.Empty, date));
        var unmark = _unmarkValidator.TestValidate(new UnmarkSubHabitCompletedCommand(Guid.Empty, Guid.Empty, date));

        result.ShouldHaveValidationErrorFor(x => x.HabitId);
        result.ShouldHaveValidationErrorFor(x => x.SubHabitId);
        unmark.ShouldHaveValidationErrorFor(x => x.HabitId);
        unmark.ShouldHaveValidationErrorFor(x => x.SubHabitId);
    }

    [Theory]
    [InlineData(1999, 12, 31)]
    [InlineData(2101, 1, 1)]
    public void ADayOutsideThePlausibleRange_IsRejected(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        _validator.TestValidate(new MarkSubHabitCompletedCommand(Guid.NewGuid(), Guid.NewGuid(), date))
            .ShouldHaveValidationErrorFor(x => x.Date);
        _unmarkValidator.TestValidate(new UnmarkSubHabitCompletedCommand(Guid.NewGuid(), Guid.NewGuid(), date))
            .ShouldHaveValidationErrorFor(x => x.Date);
    }

    [Fact]
    public void TheEdgesOfTheRange_AreAccepted()
    {
        foreach (var date in new[] { Habit.MinStartDate, Habit.MaxStartDate })
            _validator.TestValidate(new MarkSubHabitCompletedCommand(Guid.NewGuid(), Guid.NewGuid(), date))
                .ShouldNotHaveValidationErrorFor(x => x.Date);
    }
}
