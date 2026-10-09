using FluentValidation;
using HannasHabits.Application.Auth;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HannasHabits.Integration.Tests.Identity;

// The accounts, on top of the real ASP.NET Identity and the real database. Each call runs in a scope of its own, like one
// HTTP request each.
[Collection(IntegrationCollection.Name)]
public class IdentityServiceTests : IAsyncLifetime
{
    private const string Password = "Passw0rd!-test";

    private readonly TestEnvironment _environment;
    private ServiceProvider _provider = null!;
    private ApplicationDbContext _db = null!;

    public IdentityServiceTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public Task InitializeAsync()
    {
        _provider = TestServices.Build(_environment.SharedConnectionString);
        _db = _environment.CreateContext();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private async Task<T> InScope<T>(Func<IIdentityService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IIdentityService>());
    }

    private static string NewEmail() => $"identity-{Guid.NewGuid():N}@example.com";

    private Task<IdentityUserDto> Register(string email, string password = Password, string? firstName = null, string? lastName = null)
        => InScope(identity => identity.RegisterAsync(email, password, firstName, lastName));

    private Task<IdentityUserDto> Authenticate(string email, string password) => InScope(identity => identity.AuthenticateAsync(email, password));

    private Task<IdentityUserDto> External(string subject, string email, string? firstName = null, string? lastName = null)
        => InScope(identity => identity.SignInWithExternalAsync(new ExternalIdentity("Google", subject, email, firstName, lastName)));

    private static string NewSubject() => Guid.NewGuid().ToString("N");

    // ---- register ----

    [Fact]
    public async Task Register_CreatesTheAccount_WithAHashedPassword()
    {
        var email = NewEmail();

        var user = await Register(email, firstName: "  Ada ", lastName: " Lovelace  ");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.UserName);
        Assert.Equal(email, user.Email);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.NotNull(stored.PasswordHash);
        Assert.DoesNotContain(Password, stored.PasswordHash);
        Assert.False(stored.EmailConfirmed); // registration does not confirm addresses (and the Google linking rule depends on that)
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_WithoutAName_TheFirstNameFallsBackToTheEmailsLocalPart(string? name)
    {
        var local = $"grace-{Guid.NewGuid():N}";

        var user = await Register(local + "@example.com", firstName: name);

        Assert.Equal(local, user.FirstName);
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).FirstName); // stored as "none", derived on the way out
    }

    [Fact]
    public async Task Register_ATooLongName_IsCutNotRejected_TheValidatorIsWhatRefusesIt()
    {
        var user = await Register(NewEmail(), firstName: new string('n', 150), lastName: new string('l', 150));

        Assert.Equal(100, user.FirstName.Length);
        Assert.Equal(100, user.LastName!.Length);
    }

    [Fact]
    public async Task Register_OnlyALastName_ShowsTheEmailsLocalPartAsTheFirstName_ButKeepsTheLastName()
    {
        var local = $"lovelace-{Guid.NewGuid():N}";

        var user = await Register(local + "@example.com", lastName: "Lovelace");

        Assert.Equal(local, user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).FirstName);
    }

    [Fact]
    public async Task Register_WithoutALastName_TheLastNameIsNull()
    {
        var user = await Register(NewEmail(), firstName: "Ada", lastName: "  ");

        Assert.Null(user.LastName);
    }

    [Fact]
    public async Task Register_AWeakPassword_IsAValidationErrorOnPassword_AndNoAccountExists()
    {
        var email = NewEmail();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => Register(email, "abc"));

        Assert.All(exception.Errors, error => Assert.Equal("Password", error.PropertyName)); // the key the client shows it at
        Assert.True(exception.Errors.Count() >= 2); // too short, no digit, no uppercase...
        Assert.Equal(0, await Sql.CountAsync(_db, "AspNetUsers", "Email", email));
    }

    [Fact]
    public async Task Register_AnInvalidEmail_IsAValidationErrorOnEmail()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => Register("not-an-email"));

        Assert.Contains(exception.Errors, error => error.PropertyName == "Email");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_AnEmailThatIsTaken_IsAConflict_EvenInOtherCase(bool otherCase)
    {
        var email = NewEmail();
        await Register(email);

        await Assert.ThrowsAsync<ConflictException>(() => Register(otherCase ? email.ToUpperInvariant() : email));
    }

    [Fact]
    public async Task Register_EightRegistrationsOfTheSameEmailAtOnce_OneWins_TheOthersAreConflicts()
    {
        var email = NewEmail();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            try
            {
                await Register(email);
                return (Exception?)null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        })));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.All(outcomes.Where(o => o is not null), outcome => Assert.IsType<ConflictException>(outcome)); // never a raw database error
        Assert.Equal(1, await Sql.CountAsync(_db, "AspNetUsers", "Email", email));
    }

    // ---- authenticate ----

    [Fact]
    public async Task Authenticate_WithTheRightPassword_ReturnsTheAccount_EmailCaseDoesNotMatter()
    {
        var email = NewEmail();
        var registered = await Register(email, firstName: "Ada");

        var user = await Authenticate(email.ToUpperInvariant(), Password);

        Assert.Equal(registered.Id, user.Id);
        Assert.Equal("Ada", user.FirstName);
    }

    [Fact]
    public async Task Authenticate_ARightEmailWithAWrongPassword_AndAnUnknownEmail_FailTheSameWay()
    {
        var email = NewEmail();
        await Register(email);

        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Authenticate(email, "Wr0ng-password!"));
        var unknownEmail = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Authenticate(NewEmail(), "Wr0ng-password!"));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message); // the login does not reveal which emails have an account
    }

    [Fact]
    public async Task Authenticate_TheFifthWrongPasswordLocksTheAccount_AndTheRightOneIsRefusedAfterwards()
    {
        var email = NewEmail();
        await Register(email);

        for (var attempt = 1; attempt <= 4; attempt++)
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => Authenticate(email, "Wr0ng-password!"));

        await Assert.ThrowsAsync<AccountLockedOutException>(() => Authenticate(email, "Wr0ng-password!"));
        await Assert.ThrowsAsync<AccountLockedOutException>(() => Authenticate(email, Password));
        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.True(stored.LockoutEnd > DateTimeOffset.UtcNow.AddMinutes(14)); // 15 minutes
    }

    [Fact]
    public async Task Authenticate_ASuccessfulLogin_StartsTheCountOfFailuresOver()
    {
        var email = NewEmail();
        await Register(email);
        for (var attempt = 1; attempt <= 3; attempt++)
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => Authenticate(email, "Wr0ng-password!"));

        await Authenticate(email, Password);
        for (var attempt = 1; attempt <= 4; attempt++)
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => Authenticate(email, "Wr0ng-password!"));

        await Authenticate(email, Password); // 3 + success + 4 would be 7 failures in a row without the reset
    }

    // ---- sign in with an external identity (Google) ----

    [Fact]
    public async Task External_TheFirstSignIn_CreatesAConfirmedAccountWithoutPassword_AndRemembersTheLogin()
    {
        var subject = NewSubject();
        var email = NewEmail();

        var user = await External(subject, email, "Ada", "Lovelace");

        Assert.Equal(email, user.Email);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.True(stored.EmailConfirmed); // Google verified it
        Assert.Null(stored.PasswordHash);   // there is no password to guess
        Assert.Equal(1, await Sql.ScalarAsync(_db, "SELECT COUNT(*) FROM \"AspNetUserLogins\" WHERE \"LoginProvider\" = 'Google' AND \"ProviderKey\" = @p0 AND \"UserId\" = @p1", subject, user.Id));
    }

    [Fact]
    public async Task External_TheNextSignIn_FindsTheSameAccount_ByTheGoogleSubject()
    {
        var subject = NewSubject();
        var first = await External(subject, NewEmail(), "Ada");

        var second = await External(subject, NewEmail(), "Ada"); // even the email Google reports may change; the subject is the identity

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Email, second.Email);
    }

    [Fact]
    public async Task External_APasswordAccountWithTheSameEmail_IsNeverTakenOver()
    {
        var email = NewEmail();
        var passwordAccount = await Register(email);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => External(NewSubject(), email, "Not the owner"));

        Assert.Contains("password", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Sql.CountAsync(_db, "AspNetUserLogins", "UserId", passwordAccount.Id)); // no login was linked
        Assert.Equal(1, await Sql.CountAsync(_db, "AspNetUsers", "Email", email));
        await Authenticate(email, Password); // the real owner can still sign in
    }

    [Fact]
    public async Task External_AnAccountWithAnotherGoogleIdentity_AndTheSameEmail_IsAlsoAConflict()
    {
        var email = NewEmail();
        await External(NewSubject(), email, "First");

        await Assert.ThrowsAsync<ConflictException>(() => External(NewSubject(), email, "Second"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task External_WithoutAName_TheFirstNameFallsBackToTheLocalPart(string? name)
    {
        var local = $"nameless-{Guid.NewGuid():N}";

        var user = await External(NewSubject(), local + "@example.com", name);

        Assert.Equal(local, user.FirstName);
    }

    [Fact]
    public async Task External_ALongProviderName_IsCutToTheLimit_NeverInTheMiddleOfAnEmoji()
    {
        var cut = await External(NewSubject(), NewEmail(), new string('n', 150), new string('l', 150));
        var emojiOnTheBorder = await External(NewSubject(), NewEmail(), new string('a', 99) + "😀" + "tail");
        var emojiInsideTheLimit = await External(NewSubject(), NewEmail(), new string('a', 98) + "😀" + "tail");

        Assert.Equal(100, cut.FirstName.Length);
        Assert.Equal(100, cut.LastName!.Length);
        Assert.Equal(99, emojiOnTheBorder.FirstName.Length);                    // the emoji would not fit whole: it is left out
        Assert.False(char.IsHighSurrogate(emojiOnTheBorder.FirstName[^1]));      // so no half of it (invalid text) remains
        Assert.Equal(new string('a', 98) + "😀", emojiInsideTheLimit.FirstName); // 100 characters, kept whole
    }

    [Fact]
    public async Task External_AnAccountWithoutAName_GetsTheProvidersName_ButAnExistingNameIsNeverOverwritten()
    {
        var subject = NewSubject();
        var first = await External(subject, $"anon-{Guid.NewGuid():N}@example.com", firstName: null);
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == first.Id)).FirstName);

        await External(subject, first.Email, "Ada", "Lovelace");
        var backFilled = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == first.Id);
        Assert.Equal(("Ada", "Lovelace"), (backFilled.FirstName, backFilled.LastName)); // back-filled

        await External(subject, first.Email, "Somebody", "Else");
        var kept = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == first.Id);
        Assert.Equal(("Ada", "Lovelace"), (kept.FirstName, kept.LastName)); // kept
    }

    [Fact]
    public async Task External_EightFirstSignInsAtOnce_CreateOneAccount_AndEveryRequestCanGoOnToSaveItsTokens()
    {
        var subject = NewSubject();
        var email = NewEmail();

        // What the Google login does after the sign-in: issue tokens through the SAME scope (same DbContext). A failed insert
        // of the losing requests must not stay behind in that context, or this second save inserts the user again.
        async Task<(Guid? Id, Exception? Failure)> SignInAndIssueTokens()
        {
            await using var scope = _provider.CreateAsyncScope();
            try
            {
                var user = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
                    .SignInWithExternalAsync(new ExternalIdentity("Google", subject, email, "Ada"));
                await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateTokenPairAsync(user);
                return (user.Id, null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(SignInAndIssueTokens)));

        Assert.True(outcomes.All(o => o.Failure is null), string.Join(Environment.NewLine, outcomes.Where(o => o.Failure is not null).Select(o => o.Failure!.Message).Distinct()));
        Assert.Single(outcomes.Select(o => o.Id).Distinct());
        Assert.Equal(1, await Sql.CountAsync(_db, "AspNetUsers", "Email", email));
        Assert.Equal(8, await Sql.CountAsync(_db, "RefreshTokens", "UserId", outcomes[0].Id!.Value)); // one session per request
    }

    [Fact]
    public async Task External_WhenTheNameBackFillLosesARace_TheSignInStillSucceeds_AndTheContextStaysUsable()
    {
        var subject = NewSubject();
        var nameless = await External(subject, NewEmail(), firstName: null); // an old account without a display name
        var stale = new StaleConcurrencyStampInterceptor(_environment.SharedConnectionString);
        await using var provider = TestServices.Build(_environment.SharedConnectionString, clock: null, stale);
        await using var scope = provider.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        // Right before Identity writes the new name, a parallel request changes the same user row: the UPDATE finds no row.
        stale.Armed = true;
        var user = await identity.SignInWithExternalAsync(new ExternalIdentity("Google", subject, nameless.Email, "Ada Lovelace"));
        stale.Armed = false;

        Assert.Equal(nameless.Id, user.Id);                                   // the sign-in itself is fine
        Assert.True(stale.Fired);                                             // and the race really took place
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.SaveChangesAsync();                                     // the token service saves this context next: it must not choke on the failed update
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == nameless.Id)).FirstName); // nothing was written through the back door
    }

    /// <summary>Right before the user row is updated, changes its concurrency stamp from another connection.</summary>
    private sealed class StaleConcurrencyStampInterceptor : SaveChangesInterceptor
    {
        private readonly string _connectionString;

        public StaleConcurrencyStampInterceptor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public volatile bool Armed;
        public volatile bool Fired;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context is { } context)
            {
                foreach (var entry in context.ChangeTracker.Entries<Infrastructure.Identity.ApplicationUser>().Where(e => e.State == EntityState.Modified))
                {
                    await using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken);
                    await using var command = new NpgsqlCommand("UPDATE \"AspNetUsers\" SET \"ConcurrencyStamp\" = @stamp WHERE \"Id\" = @id", connection);
                    command.Parameters.AddWithValue("stamp", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("id", entry.Entity.Id);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    Fired = true;
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
