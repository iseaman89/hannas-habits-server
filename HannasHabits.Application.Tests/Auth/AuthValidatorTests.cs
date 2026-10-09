using FluentValidation.TestHelper;
using HannasHabits.Application.Auth.Commands.GoogleLogin;
using HannasHabits.Application.Auth.Commands.Login;
using HannasHabits.Application.Auth.Commands.Refresh;
using HannasHabits.Application.Auth.Commands.Register;
using HannasHabits.Application.Auth.Commands.Revoke;

namespace HannasHabits.Application.Tests.Auth;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void AValidCommand_Passes_WithOrWithoutNames()
    {
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!")).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", "Ada")).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", "Ada", "Lovelace")).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-an-email")]
    [InlineData("two@@example.com")]
    public void AMissingOrMalformedEmail_IsRejected(string email)
    {
        _validator.TestValidate(new RegisterCommand(email, "Passw0rd!")).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void TheEmail_MayHave256Characters_NotMore()
    {
        var at256 = new string('a', 256 - "@example.com".Length) + "@example.com";

        _validator.TestValidate(new RegisterCommand(at256, "Passw0rd!")).ShouldNotHaveValidationErrorFor(x => x.Email);
        _validator.TestValidate(new RegisterCommand("a" + at256, "Passw0rd!")).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void ThePassword_IsOnlyCheckedForShape_ThePolicyBelongsToIdentity()
    {
        _validator.TestValidate(new RegisterCommand("ada@example.com", "")).ShouldHaveValidationErrorFor(x => x.Password);
        _validator.TestValidate(new RegisterCommand("ada@example.com", "a")).ShouldNotHaveValidationErrorFor(x => x.Password);
        _validator.TestValidate(new RegisterCommand("ada@example.com", new string('a', 128))).ShouldNotHaveValidationErrorFor(x => x.Password);
        _validator.TestValidate(new RegisterCommand("ada@example.com", new string('a', 129))).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void TheFirstName_IsOptional_BlankIsFine_AndMayHave100Characters()
    {
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", null)).ShouldNotHaveValidationErrorFor(x => x.FirstName);
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", "   ")).ShouldNotHaveValidationErrorFor(x => x.FirstName);
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", new string('n', 100))).ShouldNotHaveValidationErrorFor(x => x.FirstName);
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", new string('n', 101))).ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void TheLastName_IsOptional_BlankIsFine_AndMayHave100Characters_IndependentlyOfTheFirstName()
    {
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", "Ada", null)).ShouldNotHaveValidationErrorFor(x => x.LastName);
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", "Ada", "   ")).ShouldNotHaveValidationErrorFor(x => x.LastName);
        _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", new string('n', 100), new string('n', 100))).ShouldNotHaveAnyValidationErrors();

        var tooLong = _validator.TestValidate(new RegisterCommand("ada@example.com", "Passw0rd!", "Ada", new string('n', 101)));
        tooLong.ShouldHaveValidationErrorFor(x => x.LastName);
        tooLong.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }
}

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void ALoginDoesNotCheckTheFormat_ItMustNotRevealWhichRuleAnExistingAccountWouldHaveFailed()
    {
        _validator.TestValidate(new LoginCommand("not-an-email", "a")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmailAndPassword_AreRequired()
    {
        var result = _validator.TestValidate(new LoginCommand("", ""));

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void TheLengthsAreCapped_SoAHugePasswordCannotCostHashingTime()
    {
        var result = _validator.TestValidate(new LoginCommand(new string('a', 257), new string('a', 129)));

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}

public class RefreshRevokeAndGoogleValidatorTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("token", true)]
    public void ARefreshToken_IsRequired(string token, bool valid)
    {
        Assert.Equal(valid, new RefreshCommandValidator().TestValidate(new RefreshCommand(token)).IsValid);
        Assert.Equal(valid, new RevokeCommandValidator().TestValidate(new RevokeCommand(token)).IsValid);
    }

    [Fact]
    public void ARefreshToken_MayHave256Characters_NotMore()
    {
        Assert.True(new RefreshCommandValidator().TestValidate(new RefreshCommand(new string('a', 256))).IsValid);
        Assert.False(new RefreshCommandValidator().TestValidate(new RefreshCommand(new string('a', 257))).IsValid);
        Assert.True(new RevokeCommandValidator().TestValidate(new RevokeCommand(new string('a', 256))).IsValid);
        Assert.False(new RevokeCommandValidator().TestValidate(new RevokeCommand(new string('a', 257))).IsValid);
    }

    [Fact]
    public void AGoogleIdToken_IsRequired_AndMayHave4096Characters()
    {
        var validator = new GoogleLoginCommandValidator();

        Assert.False(validator.TestValidate(new GoogleLoginCommand("")).IsValid);
        Assert.True(validator.TestValidate(new GoogleLoginCommand(new string('a', 4096))).IsValid);
        Assert.False(validator.TestValidate(new GoogleLoginCommand(new string('a', 4097))).IsValid);
    }
}
