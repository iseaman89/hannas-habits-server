namespace HannasHabits.Application.Auth;

/// <summary>
/// The user accounts. Implemented in Infrastructure on top of ASP.NET Identity, so Application and Domain
/// know nothing about <c>UserManager</c> or password hashing.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates an account. Throws <see cref="FluentValidation.ValidationException"/> (key <c>password</c>) if the password
    /// violates the password policy and <see cref="Common.Exceptions.ConflictException"/> if the email is taken.
    /// A blank <paramref name="firstName"/> or <paramref name="lastName"/> counts as "none given".
    /// </summary>
    Task<IdentityUserDto> RegisterAsync(string email, string password, string? firstName, string? lastName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks email + password and counts failures towards a lockout. Throws
    /// <see cref="Common.Exceptions.AuthenticationFailedException"/> for wrong credentials (deliberately the same
    /// answer for an unknown email) and <see cref="Common.Exceptions.AccountLockedOutException"/> while locked out.
    /// </summary>
    Task<IdentityUserDto> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the account that belongs to an external (Google) identity, or creates it on first sight.
    /// An existing password account with the same email is <b>not</b> taken over silently - that throws a
    /// <see cref="Common.Exceptions.ConflictException"/> (see the implementation for why).
    /// A new account takes the provider's names; an existing one only gets them if it has no name yet.
    /// </summary>
    Task<IdentityUserDto> SignInWithExternalAsync(ExternalIdentity identity, CancellationToken cancellationToken = default);
}

/// <summary>What an external identity provider vouches for after its token has been verified.</summary>
/// <param name="FirstName">The given name the provider knows; optional, Google does not always send one.</param>
/// <param name="LastName">The family name; optional.</param>
public record ExternalIdentity(string Provider, string Subject, string Email, string? FirstName = null, string? LastName = null);
