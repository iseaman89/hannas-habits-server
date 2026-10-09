using System.Reflection;

namespace HannasHabits.Architecture.Tests;

/// <summary>The four assemblies of the solution and the namespaces of the libraries the rules talk about.</summary>
internal static class Layers
{
    public static readonly Assembly Domain = typeof(Domain.Entities.Habit).Assembly;
    public static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    public static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly WebApi = typeof(WebApi.DependencyInjection).Assembly;

    public const string DomainNamespace = "HannasHabits.Domain";
    public const string ApplicationNamespace = "HannasHabits.Application";
    public const string InfrastructureNamespace = "HannasHabits.Infrastructure";
    public const string WebApiNamespace = "HannasHabits.WebApi";

    // Frameworks and libraries a layer must not know about.
    public const string EntityFramework = "Microsoft.EntityFrameworkCore";
    public const string Npgsql = "Npgsql";
    public const string AspNetCore = "Microsoft.AspNetCore";
    public const string AspNetIdentity = "Microsoft.AspNetCore.Identity";
    public const string MediatR = "MediatR";
    public const string FluentValidation = "FluentValidation";
    public const string Mapster = "Mapster";
}
