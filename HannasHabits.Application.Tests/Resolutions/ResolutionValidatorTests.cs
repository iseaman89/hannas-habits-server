using FluentValidation.TestHelper;
using HannasHabits.Application.Resolutions.Commands.AddResolution;
using HannasHabits.Application.Resolutions.Commands.UpdateResolution;
using HannasHabits.Application.Resolutions.Queries.GetResolutionsByYear;

namespace HannasHabits.Application.Tests.Resolutions;

public class AddResolutionCommandValidatorTests
{
    private readonly AddResolutionCommandValidator _validator = new();

    [Fact]
    public void AValidCommand_Passes_WithOrWithoutAHabit()
    {
        _validator.TestValidate(new AddResolutionCommand(2026, "Read", null)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new AddResolutionCommand(2026, "Read", Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2100, true)]
    [InlineData(1999, false)]
    [InlineData(2101, false)]
    [InlineData(0, false)]
    public void TheYear_MustBeBetween2000And2100(int year, bool valid)
    {
        var result = _validator.TestValidate(new AddResolutionCommand(year, "Read", null));

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.Year);
        else
            result.ShouldHaveValidationErrorFor(x => x.Year);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AMissingOrBlankTitle_IsRejected(string? title)
    {
        _validator.TestValidate(new AddResolutionCommand(2026, title!, null)).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TheTitle_MayHave200Characters_NotMore()
    {
        _validator.TestValidate(new AddResolutionCommand(2026, new string('a', 200), null)).ShouldNotHaveValidationErrorFor(x => x.Title);
        _validator.TestValidate(new AddResolutionCommand(2026, new string('a', 201), null)).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void AnAllZeroHabitId_IsRejected()
    {
        _validator.TestValidate(new AddResolutionCommand(2026, "Read", Guid.Empty)).ShouldHaveValidationErrorFor(x => x.HabitId);
    }
}

public class UpdateResolutionCommandValidatorTests
{
    private readonly UpdateResolutionCommandValidator _validator = new();

    [Fact]
    public void AValidCommand_Passes()
    {
        _validator.TestValidate(new UpdateResolutionCommand(2026, Guid.NewGuid(), "Read", true, null)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void KeptIsRequired_ForgettingItMustNotQuietlyReopenAKeptResolution()
    {
        _validator.TestValidate(new UpdateResolutionCommand(2026, Guid.NewGuid(), "Read", null, null))
            .ShouldHaveValidationErrorFor(x => x.Kept);
    }

    [Fact]
    public void YearTitleAndHabitId_FollowTheSameRulesAsOnAdd()
    {
        var result = _validator.TestValidate(new UpdateResolutionCommand(1999, Guid.NewGuid(), " ", false, Guid.Empty));

        result.ShouldHaveValidationErrorFor(x => x.Year);
        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.HabitId);
    }
}

public class GetResolutionsByYearQueryValidatorTests
{
    [Theory]
    [InlineData(2000, true)]
    [InlineData(2026, true)]
    [InlineData(2100, true)]
    [InlineData(1999, false)]
    [InlineData(2101, false)]
    public void TheYear_MustBeBetween2000And2100(int year, bool valid)
    {
        var result = new GetResolutionsByYearQueryValidator().TestValidate(new GetResolutionsByYearQuery(year));

        if (valid)
            result.ShouldNotHaveAnyValidationErrors();
        else
            result.ShouldHaveValidationErrorFor(x => x.Year);
    }
}
