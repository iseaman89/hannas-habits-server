using System.Reflection;
using HannasHabits.Application.Auth;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HannasHabits.WebApi.OpenApi;

/// <summary>
/// Marks every property of the Application's DTOs (<c>AuthResult</c>, <c>HabitOverviewDto</c>, ...) as <c>required</c>.
/// </summary>
/// <remarks>
/// Those types are what the API answers with, and the serializer always writes all of their properties - a nullable
/// one as <c>null</c>, not left out. Without <c>required</c> a generated client would type every field as optional and
/// force <c>?.</c>/<c>!</c> on values that are always there. Request models live in <c>WebApi.Models</c> and are left
/// alone on purpose: there "omitted" is part of the contract (default or cleared).
/// </remarks>
public sealed class ApplicationDtoSchemaFilter : ISchemaFilter
{
    private static readonly Assembly ApplicationAssembly = typeof(AuthResult).Assembly;

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.Assembly != ApplicationAssembly || schema.Properties.Count == 0)
            return;

        foreach (var property in schema.Properties.Keys)
            schema.Required.Add(property);
    }
}
