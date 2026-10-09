using FluentValidation;
using FluentValidation.Results;
using HannasHabits.Application.Auth;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HannasHabits.Infrastructure.Identity;

// UserManager and SignInManager take no CancellationToken, so the tokens only reach the database calls made here.
public class IdentityService : IIdentityService
{
    // The same answer for "no such email" and "wrong password": the login must not tell which emails have an account.
    private const string InvalidCredentials = "The email or password is incorrect.";
    private const string EmailInUse = "The email address is already in use.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    public async Task<IdentityUserDto> RegisterAsync(string email, string password, string? firstName, string? lastName, CancellationToken cancellationToken = default)
    {
        var user = NewUser(email, firstName, lastName);

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Two concurrent registrations passed Identity's duplicate check; the unique index stopped the second.
            throw new ConflictException(EmailInUse, exception);
        }

        ThrowIfFailed(result);

        return user.ToDto();
    }

    public async Task<IdentityUserDto> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email)
                   ?? throw new AuthenticationFailedException(InvalidCredentials);

        // lockoutOnFailure: every wrong password counts, and after the configured number of failures the account is
        // locked for a while - this is what makes guessing passwords against /login impractical.
        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            throw new AccountLockedOutException();

        if (!result.Succeeded)
            throw new AuthenticationFailedException(InvalidCredentials);

        return user.ToDto();
    }

    public async Task<IdentityUserDto> SignInWithExternalAsync(ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByLoginAsync(identity.Provider, identity.Subject);
        if (existing is not null)
        {
            await FillMissingNameAsync(existing, identity.FirstName, identity.LastName);
            return existing.ToDto();
        }

        var user = NewUser(identity.Email, identity.FirstName, identity.LastName);
        user.EmailConfirmed = true; // Google verified it (the verifier rejects tokens without a verified email)

        try
        {
            // Create the user and the Google login together: a user without login would be stuck, because the next
            // attempt would find the email taken.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            ThrowIfFailed(await _userManager.CreateAsync(user));
            ThrowIfFailed(await _userManager.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.Subject, identity.Provider)));

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is ConflictException || exception is DbUpdateException dbUpdate && IsUniqueViolation(dbUpdate))
        {
            // A failed insert stays tracked as "added" in this scope's context, and the token service saves the same
            // context right after us: it would insert the user again and fail with the same unique violation.
            _db.ChangeTracker.Clear();

            // The email is taken - either by the same person (their first Google sign-in ran twice at once and the
            // other request was faster; Identity's duplicate check or the unique index tells us) or by somebody else.
            var winner = await _userManager.FindByLoginAsync(identity.Provider, identity.Subject);
            if (winner is not null)
                return winner.ToDto();

            // No silent linking by email. Registration does not confirm email addresses, so anybody could have created
            // a password account for somebody else's address in advance; linking the real owner's Google login to it
            // would hand the account (and everything they enter) to whoever knows that password. Linking has to be an
            // explicit step of a signed-in user, or has to wait until emails are confirmed.
            throw new ConflictException("An account with this email address already exists. Sign in with your password instead.", exception);
        }

        return user.ToDto();
    }

    private static ApplicationUser NewUser(string email, string? firstName, string? lastName)
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email };
        user.SetName(firstName, lastName);
        return user;
    }

    // Accounts created by a Google sign-in before names existed have none. The name is cosmetic, so this is best
    // effort: it is only filled in, never overwritten (the user may choose their own name later), and a failed update -
    // e.g. a parallel sign-in that was faster - must not fail the login. The next sign-in tries again.
    private async Task FillMissingNameAsync(ApplicationUser user, string? providerFirstName, string? providerLastName)
    {
        if (user.HasName)
            return;

        user.SetName(providerFirstName, providerLastName);
        if (!user.HasName)
            return;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            // The failed update leaves the user "modified" in this scope's context; the token service saves the same
            // context right after us and would trip over it (and write the name through the back door).
            _db.Entry(user).State = EntityState.Detached;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    // Identity reports problems as codes; they become the same exceptions the rest of the application throws, so the
    // GlobalExceptionHandler turns them into 409 / 400 with the usual (camelCase) error keys.
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
            return;

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            throw new ConflictException(EmailInUse);

        var failures = result.Errors.Select(error => new ValidationFailure(PropertyOf(error), error.Description)).ToList();

        // Anything that is neither a duplicate nor a rule the caller can fix (e.g. a concurrency failure) is not a 400.
        if (failures.Any(f => f.PropertyName.Length == 0))
            throw new InvalidOperationException("Identity operation failed: " + string.Join("; ", result.Errors.Select(e => e.Code)));

        throw new ValidationException(failures);
    }

    private static string PropertyOf(IdentityError error) => error.Code switch
    {
        _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => "Password",
        nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName) => "Email",
        _ => string.Empty
    };
}
