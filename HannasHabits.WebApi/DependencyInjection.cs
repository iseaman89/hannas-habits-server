using System.Text;
using HannasHabits.Infrastructure.Identity;
using HannasHabits.WebApi.ExceptionHandling;
using HannasHabits.WebApi.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace HannasHabits.WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        // FluentValidation (ValidationBehaviour) is the one validation path. Without this switch MVC additionally
        // treats every non-nullable reference property of a request as [Required] and answers a missing property with
        // its own error (key "Title" instead of "title") before the command is even created.
        services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        AddJwtAuthentication(services);
        services.AddAuthorization();

        AddCors(services, configuration);
        AddSwagger(services);

        return services;
    }

    private static void AddJwtAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();

        // Configured from the validated JwtOptions (same values the token service signs with).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
                };
            });
    }

    // Allowed origins come from config (Cors:AllowedOrigins; production: env var Cors__AllowedOrigins__0, ...).
    // No origins configured = no cross-origin access.
    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }

    private static void AddSwagger(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // A generated client should see what the C# types say: `string` is never null, `string?` can be.
            options.SupportNonNullableReferenceTypes();

            // OpenAPI 3.0 cannot put `nullable` next to a `$ref`, so `Mood? Mood` would lose its nullability.
            // allOf: [$ref] + nullable keeps it.
            options.UseAllOfToExtendReferenceSchemas();

            options.SchemaFilter<ApplicationDtoSchemaFilter>();

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Description = "Paste the access token only - Swagger adds the \"Bearer \" prefix.",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            // The requirement refers to the definition above by id (a definition must not carry the reference itself).
            var bearerReference = new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearerReference] = [] });
        });
    }
}
