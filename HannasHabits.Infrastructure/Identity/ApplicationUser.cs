using HannasHabits.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace HannasHabits.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>The first name the user gave (register form) or Google sent; <c>null</c> = none. Use <see cref="GetFirstName"/> to show it.</summary>
    public string? FirstName { get; private set; }

    /// <summary>The last name; <c>null</c> = none. Kept for the account, the UI does not show it (yet).</summary>
    public string? LastName { get; private set; }

    /// <summary>Whether the account has any name at all - Google's name is only back-filled into an account that has none.</summary>
    public bool HasName => FirstName is not null || LastName is not null;

    /// <summary>
    /// Sets both names. Each is trimmed and a blank one becomes <c>null</c>. A name that is too long is cut instead of
    /// rejected: the register validator already refuses it, so only a provider's name (which we cannot make shorter)
    /// gets here, and a sign-in must not fail because of it.
    /// </summary>
    public void SetName(string? firstName, string? lastName)
    {
        FirstName = Clean(firstName);
        LastName = Clean(lastName);
    }

    /// <summary>What the UI calls the person: the first name, else the part of the email before the "@".</summary>
    public string GetFirstName()
    {
        if (FirstName is not null)
            return FirstName;

        var email = Email ?? "";
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }

    private static string? Clean(string? name)
    {
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
            return null;

        const int max = AuthLimits.NameMaxLength;
        if (name.Length <= max)
            return name;

        // Never cut between the two halves of a surrogate pair (an emoji): a lone surrogate is not valid UTF-8.
        var length = char.IsHighSurrogate(name[max - 1]) ? max - 1 : max;
        return name[..length].TrimEnd();
    }
}
