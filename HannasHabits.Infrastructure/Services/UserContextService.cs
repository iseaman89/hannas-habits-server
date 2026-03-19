using System.Security.Claims;
using HannasHabits.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HannasHabits.Infrastructure.Services;

public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _contextAccessor;

    public UserContextService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var id = _contextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? _contextAccessor.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return id != null ? Guid.Parse(id) : null;
        }
    }

    public string? Username =>
        _contextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
        ?? _contextAccessor.HttpContext?.User?.Identity?.Name;
}