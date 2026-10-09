using NetArchTest.Rules;

namespace HannasHabits.Architecture.Tests;

// A "must not depend on X" rule passes just as happily when the analysis finds nothing at all. These checks prove
// that it does find dependencies - where we know there are some - so a green rule means something.
public class RuleEngineSanityTests
{
    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    [InlineData("Infrastructure")]
    [InlineData("WebApi")]
    public void EveryLayerHasTypes(string layer)
    {
        var assembly = layer switch
        {
            "Domain" => Layers.Domain,
            "Application" => Layers.Application,
            "Infrastructure" => Layers.Infrastructure,
            _ => Layers.WebApi
        };

        Assert.True(Types.InAssembly(assembly).GetTypes().Count() > 5);
    }

    [Fact]
    public void InfrastructureDoesUseEntityFramework_Npgsql_AndAspNetIdentity()
    {
        Assert.NotEmpty(Types.InAssembly(Layers.Infrastructure).That().HaveDependencyOn(Layers.EntityFramework).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Infrastructure).That().HaveDependencyOn(Layers.Npgsql).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Infrastructure).That().HaveDependencyOn(Layers.AspNetIdentity).GetTypes());
    }

    [Fact]
    public void ApplicationDoesUseMediatR_FluentValidation_AndMapster()
    {
        Assert.NotEmpty(Types.InAssembly(Layers.Application).That().HaveDependencyOn(Layers.MediatR).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Application).That().HaveDependencyOn(Layers.FluentValidation).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Application).That().HaveDependencyOn(Layers.Mapster).GetTypes());
    }

    [Fact]
    public void ApplicationDoesUseTheDomain_AndTheWebApiDoesUseTheApplication()
    {
        Assert.NotEmpty(Types.InAssembly(Layers.Application).That().HaveDependencyOn(Layers.DomainNamespace).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.WebApi).That().HaveDependencyOn(Layers.ApplicationNamespace).GetTypes());
        Assert.NotEmpty(Types.InAssembly(Layers.Infrastructure).That().HaveDependencyOn(Layers.ApplicationNamespace).GetTypes());
    }

    [Fact]
    public void TheWebApiDoesUseInfrastructure_InTheCompositionRoot()
    {
        // JwtOptions is read where the JWT bearer authentication is configured - the one allowed place.
        Assert.NotEmpty(Types.InAssembly(Layers.WebApi).That().HaveDependencyOn(Layers.InfrastructureNamespace).GetTypes());
    }
}
