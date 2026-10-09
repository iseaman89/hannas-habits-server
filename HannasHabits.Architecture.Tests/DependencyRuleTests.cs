using System.Reflection;
using NetArchTest.Rules;

namespace HannasHabits.Architecture.Tests;

// The dependency rule of Clean Architecture: dependencies point inwards only.
//
//     WebApi ──► Application ──► Domain
//        └─────► Infrastructure ──► Application
//
// Two views on the same rule: which assemblies an assembly references (what the project files allow), and which
// namespaces its types actually use (what the code does).
public class DependencyRuleTests
{
    private static string Describe(TestResult result)
        => "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);

    private static void AssertNoDependencyOn(Assembly assembly, params string[] namespaces)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(namespaces).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(name => name.Name!).ToArray();

    // ---- assembly references ----

    [Fact]
    public void Domain_ReferencesNothingButTheFrameworkBaseLibrary()
    {
        var references = ReferencedAssemblyNames(Layers.Domain);

        Assert.All(references, name => Assert.True(
            name is "mscorlib" or "netstandard" || name.StartsWith("System.", StringComparison.Ordinal) || name == "System",
            $"Domain references '{name}'"));
    }

    [Fact]
    public void Application_ReferencesDomain_ButNotInfrastructureOrWebApi()
    {
        var references = ReferencedAssemblyNames(Layers.Application);

        Assert.Contains("HannasHabits.Domain", references);
        Assert.DoesNotContain("HannasHabits.Infrastructure", references);
        Assert.DoesNotContain("HannasHabits.WebApi", references);
    }

    [Fact]
    public void Application_DoesNotReferenceEntityFrameworkOrAspNetCore()
    {
        var references = ReferencedAssemblyNames(Layers.Application);

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_ReferencesApplication_ButNotWebApi()
    {
        var references = ReferencedAssemblyNames(Layers.Infrastructure);

        Assert.Contains("HannasHabits.Application", references);
        Assert.DoesNotContain("HannasHabits.WebApi", references);
    }

    // ---- types ----

    [Fact]
    public void Domain_DoesNotUseTheOtherLayersOrAnyFramework()
    {
        AssertNoDependencyOn(Layers.Domain,
            Layers.ApplicationNamespace, Layers.InfrastructureNamespace, Layers.WebApiNamespace,
            Layers.EntityFramework, Layers.Npgsql, Layers.AspNetCore, Layers.MediatR, Layers.FluentValidation, Layers.Mapster);
    }

    [Fact]
    public void Application_DoesNotUseInfrastructure_WebApi_EntityFramework_OrAspNet()
    {
        AssertNoDependencyOn(Layers.Application,
            Layers.InfrastructureNamespace, Layers.WebApiNamespace,
            Layers.EntityFramework, Layers.Npgsql, Layers.AspNetCore);
    }

    [Fact]
    public void Infrastructure_DoesNotUseTheWebApi()
    {
        AssertNoDependencyOn(Layers.Infrastructure, Layers.WebApiNamespace);
    }

    [Fact]
    public void OnlyInfrastructure_KnowsEntityFramework_AspNetIdentity_AndNpgsql()
    {
        AssertNoDependencyOn(Layers.WebApi, Layers.EntityFramework, Layers.Npgsql, Layers.AspNetIdentity);
    }

    [Fact]
    public void TheWebApi_DoesNotReachIntoInfrastructure_ExceptForOptionsItConfigures()
    {
        // The composition root may bind JwtOptions (a plain options class); controllers and models may not know
        // Infrastructure at all.
        var result = Types.InAssembly(Layers.WebApi)
            .That().ResideInNamespaceStartingWith("HannasHabits.WebApi.Controllers")
            .Or().ResideInNamespaceStartingWith("HannasHabits.WebApi.Models")
            .ShouldNot().HaveDependencyOn(Layers.InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }
}
