using HannasHabits.Application.Auth;

namespace HannasHabits.Infrastructure.Identity;

internal static class ApplicationUserMapping
{
    // One place for the mapping, so login, refresh and register can never disagree about what a user looks like.
    public static IdentityUserDto ToDto(this ApplicationUser user) =>
        new(user.Id, user.UserName ?? user.Email ?? "", user.Email ?? "", user.GetDisplayName());
}
