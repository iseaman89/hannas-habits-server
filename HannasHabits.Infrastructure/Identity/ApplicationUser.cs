using HannasHabits.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace HannasHabits.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>The name the user chose (register form) or Google sent; <c>null</c> = none. Use <see cref="GetDisplayName"/> to show it.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>
    /// Trims the name; blank becomes <c>null</c>. A name that is too long is cut instead of rejected: the register
    /// validator already refuses it, so only a provider's name (which we cannot make shorter) gets here, and a
    /// sign-in must not fail because of it.
    /// </summary>
    public void SetDisplayName(string? name)
    {
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            DisplayName = null;
            return;
        }

        const int max = AuthLimits.DisplayNameMaxLength;
        if (name.Length > max)
        {
            // Never cut between the two halves of a surrogate pair (an emoji): a lone surrogate is not valid UTF-8.
            var length = char.IsHighSurrogate(name[max - 1]) ? max - 1 : max;
            name = name[..length].TrimEnd();
        }

        DisplayName = name;
    }

    /// <summary>What the UI shows: the chosen name, else the part of the email before the "@".</summary>
    public string GetDisplayName()
    {
        if (DisplayName is not null)
            return DisplayName;

        var email = Email ?? "";
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }
}
