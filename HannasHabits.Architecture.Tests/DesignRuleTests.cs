using System.Reflection;
using FluentValidation;
using HannasHabits.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace HannasHabits.Architecture.Tests;

// The conventions of CLAUDE.md as executable rules: valid-by-construction domain objects, thin controllers,
// one handler per use case.
public class DesignRuleTests
{
    private static IEnumerable<Type> TypesIn(Assembly assembly, string @namespace) => assembly.GetTypes()
        .Where(type => type.Namespace == @namespace && !type.IsNested && !IsCompilerGenerated(type));

    private static bool IsCompilerGenerated(Type type) => type.Name.StartsWith('<');

    private static string Names(IEnumerable<Type> types) => string.Join(", ", types.Select(t => t.Name));

    // ---- Domain: valid by construction ----

    [Fact]
    public void ValueObjects_AreSealed_AndCannotBeConstructedFromOutside()
    {
        var valueObjects = TypesIn(Layers.Domain, "HannasHabits.Domain.ValueObjects").ToList();
        Assert.NotEmpty(valueObjects);

        var notSealed = valueObjects.Where(t => !t.IsSealed).ToList();
        var publicConstructors = valueObjects.Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0).ToList();

        Assert.True(notSealed.Count == 0, $"Not sealed: {Names(notSealed)}");
        Assert.True(publicConstructors.Count == 0, $"Public constructors (use a Create factory): {Names(publicConstructors)}");
    }

    [Fact]
    public void ValueObjects_AreImmutable()
    {
        foreach (var type in TypesIn(Layers.Domain, "HannasHabits.Domain.ValueObjects"))
        {
            var mutable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true } set && !IsInitOnly(set))
                .ToList();

            Assert.True(mutable.Count == 0, $"{type.Name} has public setters: {string.Join(", ", mutable.Select(p => p.Name))}");
        }
    }

    [Fact]
    public void Entities_AreCreatedThroughFactories_AndChangeOnlyThroughTheirOwnMethods()
    {
        var entities = Layers.Domain.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(EntityBase).IsAssignableFrom(t))
            .ToList();
        Assert.True(entities.Count >= 4, "expected Habit, HabitRecord, DailyDiary and Resolution");

        foreach (var entity in entities)
        {
            Assert.True(entity.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0,
                $"{entity.Name} has a public constructor");

            var publicSetters = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Select(p => p.Name)
                .ToList();

            Assert.True(publicSetters.Count == 0, $"{entity.Name} has public setters: {string.Join(", ", publicSetters)}");
        }
    }

    [Fact]
    public void TheAggregateExposesItsRecordsOnlyAsAReadOnlyView()
    {
        var records = typeof(Domain.Entities.Habit).GetProperty(nameof(Domain.Entities.Habit.Records))!;

        Assert.True(records.PropertyType.IsAssignableFrom(typeof(List<Domain.Entities.HabitRecord>)));
        Assert.DoesNotContain(records.PropertyType.GetInterfaces().Append(records.PropertyType),
            type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>));
    }

    private static bool IsInitOnly(MethodInfo setter) => setter.ReturnParameter
        .GetRequiredCustomModifiers()
        .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

    // ---- Application: one handler per use case ----

    private static IEnumerable<Type> ConcreteTypes(Assembly assembly) => assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && !IsCompilerGenerated(t));

    private static bool Implements(Type type, Type openGeneric) => type.GetInterfaces()
        .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);

    [Fact]
    public void Requests_AreNamedCommandOrQuery()
    {
        var requests = ConcreteTypes(Layers.Application).Where(t => Implements(t, typeof(IRequest<>))).ToList();
        Assert.NotEmpty(requests);

        var misnamed = requests.Where(t => !t.Name.EndsWith("Command") && !t.Name.EndsWith("Query")).ToList();

        Assert.True(misnamed.Count == 0, $"Neither Command nor Query: {Names(misnamed)}");
    }

    [Fact]
    public void Handlers_AreNamedAfterTheirRequest_AndLiveNextToIt()
    {
        var handlers = ConcreteTypes(Layers.Application).Where(t => Implements(t, typeof(IRequestHandler<,>))).ToList();
        Assert.NotEmpty(handlers);

        foreach (var handler in handlers)
        {
            var request = handler.GetInterfaces()
                .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .GetGenericArguments()[0];

            Assert.Equal(request.Name + "Handler", handler.Name);
            Assert.Equal(request.Namespace, handler.Namespace);
        }
    }

    [Fact]
    public void Validators_AreNamedAfterTheirRequest_AndLiveNextToIt()
    {
        var validators = ConcreteTypes(Layers.Application)
            .Where(t => t.BaseType is { IsGenericType: true } b && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .ToList();
        Assert.NotEmpty(validators);

        foreach (var validator in validators)
        {
            var request = validator.BaseType!.GetGenericArguments()[0];

            Assert.Equal(request.Name + "Validator", validator.Name);
            Assert.Equal(request.Namespace, validator.Namespace);
        }
    }

    [Fact]
    public void Handlers_DoNotDependOnOtherHandlers()
    {
        // A use case that needs a rule shares it through the Domain, not by calling a neighbouring handler.
        var handlers = ConcreteTypes(Layers.Application).Where(t => Implements(t, typeof(IRequestHandler<,>))).ToList();
        Assert.NotEmpty(handlers);

        foreach (var handler in handlers)
        {
            var others = handlers.Where(h => h != handler).Select(h => h.FullName!).ToArray();

            var result = Types.InAssembly(Layers.Application)
                .That().HaveName(handler.Name)
                .ShouldNot().HaveDependencyOnAny(others)
                .GetResult();

            Assert.True(result.IsSuccessful, $"{handler.Name} depends on another handler");
        }
    }

    // ---- WebApi: thin controllers ----

    [Fact]
    public void Controllers_OnlyTalkToTheMediator()
    {
        var controllers = Layers.WebApi.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        Assert.True(controllers.Count >= 5, "expected the five controllers");

        foreach (var controller in controllers)
        {
            var constructor = Assert.Single(controller.GetConstructors());

            Assert.True(constructor.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(IMediator)]),
                $"{controller.Name} must depend on IMediator and nothing else");
        }
    }

    [Fact]
    public void Controllers_AreAllAuthorizedOrExplicitlyAnonymous()
    {
        // Data isolation starts with "there is a user": a controller without [Authorize] must be a conscious decision.
        // AuthController is the one with public endpoints (register/login/google/refresh) and authorizes the rest per action.
        var controllers = Layers.WebApi.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();

        foreach (var controller in controllers.Where(c => c.Name != "AuthController"))
            Assert.True(controller.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true).Any(),
                $"{controller.Name} has no [Authorize]");
    }

    [Fact]
    public void Controllers_DoNotKnowTheDomainEntities()
    {
        var result = Types.InAssembly(Layers.WebApi)
            .That().ResideInNamespaceStartingWith("HannasHabits.WebApi.Controllers")
            .ShouldNot().HaveDependencyOn("HannasHabits.Domain.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, "Controllers using entities: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
