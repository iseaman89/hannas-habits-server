using HannasHabits.Application.Auth;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.Habits;
using HannasHabits.Application.Resolutions;
using HannasHabits.Infrastructure.Identity;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Infrastructure.Queries;
using HannasHabits.Infrastructure.Repositories;
using HannasHabits.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HannasHabits.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DbConnection")));
        
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Wrong passwords count per account; after 5 failures the account is locked for 15 minutes.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GoogleOptions>()
            .Bind(configuration.GetSection(GoogleOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Injected wherever "now" matters, so tests can control the clock.
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // The DbContext is the unit of work: one instance per request, shared by repositories and queries.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IHabitRepository, HabitRepository>();
        services.AddScoped<IDailyDiaryRepository, DailyDiaryRepository>();
        services.AddScoped<IResolutionRepository, ResolutionRepository>();
        services.AddScoped<IHabitQueries, HabitQueries>();
        services.AddScoped<IDailyDiaryQueries, DailyDiaryQueries>();
        services.AddScoped<IResolutionQueries, ResolutionQueries>();

        services.AddHttpContextAccessor();

        return services;
    }
}