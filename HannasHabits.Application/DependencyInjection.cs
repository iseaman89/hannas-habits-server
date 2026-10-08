using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using HannasHabits.Application.Common.Behaviors;
using MediatR;

namespace HannasHabits.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // MediatR (free Community license key; without it MediatR only logs a warning).
        // An empty value counts as "no key": docker-compose passes an unset variable on as "", and MediatR would
        // log that as a license error.
        var licenseKey = configuration["MediatR:LicenseKey"];
        services.AddMediatR(cnf =>
        {
            cnf.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cnf.LicenseKey = string.IsNullOrWhiteSpace(licenseKey) ? null : licenseKey;
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(Assembly.GetExecutingAssembly());
        services.AddSingleton(config);
        
        services.AddScoped<IMapper, ServiceMapper>();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        return services;
    }
}