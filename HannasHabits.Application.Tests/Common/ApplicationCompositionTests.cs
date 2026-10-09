using FluentValidation;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Tests.Fakes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HannasHabits.Application.Tests.Common;

// The Application layer wired up the way the real host does it (AddApplication), with fakes for what Infrastructure
// would provide: does a request really pass through the validation behaviour on its way to its handler?
public class ApplicationCompositionTests
{
    private static readonly Type[] RequestTypes = typeof(DependencyInjection).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: false, IsInterface: false } && ResponseTypeOf(type) is not null)
        .ToArray();

    private static Type? ResponseTypeOf(Type requestType) => requestType.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))?
        .GetGenericArguments()[0];

    [Fact]
    public void EveryCommandAndQueryHasARegisteredHandler()
    {
        var services = new ServiceCollection();
        services.AddApplication(new ConfigurationBuilder().Build());

        Assert.NotEmpty(RequestTypes);

        foreach (var requestType in RequestTypes)
        {
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, ResponseTypeOf(requestType)!);

            Assert.True(services.Any(descriptor => descriptor.ServiceType == handlerType),
                $"{requestType.Name} has no registered handler");
        }
    }

    [Fact]
    public void EveryRequestHasExactlyOneHandlerClass()
    {
        var handlers = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(i => (Request: i.GetGenericArguments()[0], Handler: type)))
            .ToList();

        foreach (var requestType in RequestTypes)
            Assert.True(handlers.Count(h => h.Request == requestType) == 1, $"{requestType.Name} needs exactly one handler");
    }

    private static ServiceProvider BuildProvider(FakeHabitRepository habits, FakeUnitOfWork unitOfWork)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddApplication(new ConfigurationBuilder().Build());

        services.AddSingleton<IHabitRepository>(habits);
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser());
        services.AddSingleton<TimeProvider>(TestClock.Create());

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AValidCommand_GoesThroughThePipelineToItsHandler()
    {
        var habits = new FakeHabitRepository();
        var unitOfWork = new FakeUnitOfWork();
        await using var provider = BuildProvider(habits, unitOfWork);
        var mediator = provider.GetRequiredService<IMediator>();

        var dto = await mediator.Send(new CreateHabitCommand { Title = "Read" });

        Assert.Equal("Read", dto.Title);
        Assert.Single(habits.Habits);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnInvalidCommand_IsStoppedByTheValidatorBeforeTheHandlerRuns()
    {
        var habits = new FakeHabitRepository();
        var unitOfWork = new FakeUnitOfWork();
        await using var provider = BuildProvider(habits, unitOfWork);
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new CreateHabitCommand { Title = "", Schedule = [] }));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Title");
        Assert.Contains(exception.Errors, e => e.PropertyName == "Schedule");
        Assert.Empty(habits.Habits);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExceptionsOfTheHandlerComeThroughUnchanged()
    {
        var habits = new FakeHabitRepository();
        await using var provider = BuildProvider(habits, new FakeUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() => provider.GetRequiredService<IMediator>()
            .Send(new HannasHabits.Application.Habits.Commands.DeleteHabit.DeleteHabitCommand(Guid.NewGuid())));
    }
}
