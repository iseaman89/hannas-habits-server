using FluentValidation.TestHelper;
using HannasHabits.Application.HabitDays.Commands.AttachTag;
using HannasHabits.Application.HabitDays.Commands.DetachTag;
using HannasHabits.Application.Tags.Commands.CreateTag;
using HannasHabits.Application.Tags.Commands.RenameTag;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tests.Tags;

public class CreateTagCommandValidatorTests
{
    private readonly CreateTagCommandValidator _validator = new();

    [Theory]
    [InlineData("krank")]
    [InlineData("a")]
    public void AValidName_Passes(string name)
    {
        _validator.TestValidate(new CreateTagCommand(name)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingOrBlankName_IsRejected(string? name)
    {
        _validator.TestValidate(new CreateTagCommand(name)).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void TheName_MayHave30Characters_NotMore()
    {
        _validator.TestValidate(new CreateTagCommand(new string('a', TagName.MaxLength))).ShouldNotHaveValidationErrorFor(x => x.Name);
        _validator.TestValidate(new CreateTagCommand(new string('a', TagName.MaxLength + 1))).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void TheLength_IsCountedAfterTrimming_LikeTheDomainDoesIt()
    {
        var padded = "  " + new string('a', TagName.MaxLength) + "  ";

        _validator.TestValidate(new CreateTagCommand(padded)).ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}

public class RenameTagCommandValidatorTests
{
    private readonly RenameTagCommandValidator _validator = new();

    [Fact]
    public void AValidCommand_Passes()
    {
        _validator.TestValidate(new RenameTagCommand(Guid.NewGuid(), "Urlaub")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AnEmptyId_IsRejected()
    {
        _validator.TestValidate(new RenameTagCommand(Guid.Empty, "Urlaub")).ShouldHaveValidationErrorFor(x => x.TagId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void ABlankName_IsRejected(string? name)
    {
        _validator.TestValidate(new RenameTagCommand(Guid.NewGuid(), name)).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void ATooLongName_IsRejected()
    {
        _validator.TestValidate(new RenameTagCommand(Guid.NewGuid(), new string('a', 31))).ShouldHaveValidationErrorFor(x => x.Name);
    }
}

public class AttachAndDetachTagCommandValidatorTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    [Fact]
    public void AValidCommand_Passes()
    {
        new AttachTagCommandValidator().TestValidate(new AttachTagCommand(Guid.NewGuid(), Day, Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();
        new DetachTagCommandValidator().TestValidate(new DetachTagCommand(Guid.NewGuid(), Day, Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyIds_AreRejected()
    {
        var attach = new AttachTagCommandValidator().TestValidate(new AttachTagCommand(Guid.Empty, Day, Guid.Empty));
        attach.ShouldHaveValidationErrorFor(x => x.HabitId);
        attach.ShouldHaveValidationErrorFor(x => x.TagId);

        var detach = new DetachTagCommandValidator().TestValidate(new DetachTagCommand(Guid.Empty, Day, Guid.Empty));
        detach.ShouldHaveValidationErrorFor(x => x.HabitId);
        detach.ShouldHaveValidationErrorFor(x => x.TagId);
    }

    [Theory]
    [InlineData(2000, 1, 1, true)]
    [InlineData(2100, 12, 31, true)]
    [InlineData(1999, 12, 31, false)]
    [InlineData(2101, 1, 1, false)]
    [InlineData(1, 1, 1, false)]
    public void TheDay_MustBeInThePlausibleRange(int year, int month, int day, bool valid)
    {
        var date = new DateOnly(year, month, day);

        var attach = new AttachTagCommandValidator().TestValidate(new AttachTagCommand(Guid.NewGuid(), date, Guid.NewGuid()));
        var detach = new DetachTagCommandValidator().TestValidate(new DetachTagCommand(Guid.NewGuid(), date, Guid.NewGuid()));

        Assert.Equal(valid, attach.IsValid);
        Assert.Equal(valid, detach.IsValid);
    }
}
