using System.Security.Claims;
using HannasHabits.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HannasHabits.Infrastructure.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _contextAccessor;

    public CurrentUser(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var user = _contextAccessor.HttpContext?.User;
            var id = user?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(id, out var userId) ? userId : throw new UnauthorizedAccessException();
        }
    }
}
