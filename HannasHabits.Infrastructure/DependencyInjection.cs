using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.Habits;
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
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // The DbContext is the unit of work: one instance per request, shared by repositories and queries.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IHabitRepository, HabitRepository>();
        services.AddScoped<IDailyDiaryRepository, DailyDiaryRepository>();
        services.AddScoped<IHabitQueries, HabitQueries>();
        services.AddScoped<IDailyDiaryQueries, DailyDiaryQueries>();

        services.AddHttpContextAccessor();

        return services;
    }
}