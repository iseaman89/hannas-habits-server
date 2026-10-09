using FluentValidation;
using HannasHabits.Application.Common.Behaviors;
using MediatR;

namespace HannasHabits.Application.Tests.Common;

public class ValidationBehaviourTests
{
    private sealed record Request(string Name, int Age) : IRequest<string>;

    private static InlineValidator<Request> NameRequired()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Name).NotEmpty();
        return validator;
    }

    private static InlineValidator<Request> AgeAtLeast18()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
        return validator;
    }

    private static async Task<(string Result, int HandlerCalls)> Run(Request request, params IValidator<Request>[] validators)
    {
        var handlerCalls = 0;
        var behaviour = new ValidationBehaviour<Request, string>(validators);

        var result = await behaviour.Handle(request, _ =>
        {
            handlerCalls++;
            return Task.FromResult("handled");
        }, CancellationToken.None);

        return (result, handlerCalls);
    }

    [Fact]
    public async Task WithoutValidators_TheRequestGoesStraightToTheHandler()
    {
        var (result, calls) = await Run(new Request("", 0));

        Assert.Equal("handled", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AValidRequest_ReachesTheHandler_AndItsAnswerIsReturned()
    {
        var (result, calls) = await Run(new Request("Ada", 30), NameRequired(), AgeAtLeast18());

        Assert.Equal("handled", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AnInvalidRequest_ThrowsAValidationException_AndTheHandlerIsNeverCalled()
    {
        var handlerCalls = 0;
        var behaviour = new ValidationBehaviour<Request, string>([NameRequired()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behaviour.Handle(new Request("", 30), _ =>
        {
            handlerCalls++;
            return Task.FromResult("handled");
        }, CancellationToken.None));

        Assert.Equal("Name", Assert.Single(exception.Errors).PropertyName);
        Assert.Equal(0, handlerCalls);
    }

    [Fact]
    public async Task TheFailuresOfAllValidatorsAreReportedTogether()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => Run(new Request("", 3), NameRequired(), AgeAtLeast18()));

        Assert.Equal(["Age", "Name"], exception.Errors.Select(e => e.PropertyName).Order());
    }
}
