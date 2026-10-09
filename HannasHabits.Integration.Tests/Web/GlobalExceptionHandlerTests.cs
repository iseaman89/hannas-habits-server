using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Domain.Exceptions;
using HannasHabits.WebApi.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HannasHabits.Integration.Tests.Web;

// Every exception the application knows becomes one well-defined answer (RFC 9457 problem details); the rest becomes a
// 500 that says nothing about the inside. Most of these are also visible through the API tests; the cases no controller
// can trigger today (403, 500) are only here.
public class GlobalExceptionHandlerTests
{
    private static async Task<(HttpStatusCode Status, JsonElement Body, bool Handled)> Handle(Exception exception)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        await using var provider = services.BuildServiceProvider();

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(), NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, default);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return ((HttpStatusCode)context.Response.StatusCode, body.RootElement.Clone(), handled);
    }

    [Theory]
    [InlineData(typeof(DomainException), HttpStatusCode.BadRequest)]
    [InlineData(typeof(NotFoundException), HttpStatusCode.NotFound)]
    [InlineData(typeof(ConflictException), HttpStatusCode.Conflict)]
    [InlineData(typeof(DuplicateEntryException), HttpStatusCode.Conflict)]
    [InlineData(typeof(ForbiddenException), HttpStatusCode.Forbidden)]
    [InlineData(typeof(AuthenticationFailedException), HttpStatusCode.Unauthorized)]
    [InlineData(typeof(AccountLockedOutException), HttpStatusCode.TooManyRequests)]
    [InlineData(typeof(UnauthorizedAccessException), HttpStatusCode.Unauthorized)]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.InternalServerError)]
    public async Task EachKindOfException_HasItsOwnStatus_AndIsAlwaysHandled(Type type, HttpStatusCode expected)
    {
        var exception = type.Name switch
        {
            nameof(DomainException) => new DomainException("rule broken"),
            nameof(NotFoundException) => new NotFoundException("Habit", Guid.Empty),
            nameof(ConflictException) => new ConflictException("conflict"),
            nameof(DuplicateEntryException) => new DuplicateEntryException(new Exception("unique")),
            nameof(ForbiddenException) => new ForbiddenException("no"),
            nameof(AuthenticationFailedException) => new AuthenticationFailedException("bad"),
            nameof(AccountLockedOutException) => (Exception)new AccountLockedOutException(),
            nameof(UnauthorizedAccessException) => new UnauthorizedAccessException(),
            _ => new InvalidOperationException("boom")
        };

        var (status, body, handled) = await Handle(exception);

        Assert.True(handled);
        Assert.Equal(expected, status);
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task AnUnexpectedException_IsA500_ThatLeaksNothing()
    {
        var (status, body, _) = await Handle(new InvalidOperationException("Host=db.internal;Password=hunter2 refused the connection"));

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.DoesNotContain("hunter2", body.GetRawText());
        Assert.DoesNotContain("db.internal", body.GetRawText());
        Assert.False(body.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String);
        Assert.False(body.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task AnAuthenticationRequiredAnswer_HasNoDetail()
    {
        var (_, body, _) = await Handle(new UnauthorizedAccessException("internal reason"));

        Assert.DoesNotContain("internal reason", body.GetRawText());
    }

    [Fact]
    public async Task TheMessagesOfBusinessErrors_AreShownToTheClient()
    {
        Assert.Equal("The habit is already completed.", (await Handle(new DomainException("The habit is already completed."))).Body.GetProperty("detail").GetString());
        Assert.Contains("was not found", (await Handle(new NotFoundException("Habit", Guid.NewGuid()))).Body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ValidationErrors_AreGroupedByField_WithCamelCaseKeys_AtEverySegmentOfThePath()
    {
        var failures = new[]
        {
            new ValidationFailure("Title", "Title is required."),
            new ValidationFailure("Title", "Title is too long."),
            new ValidationFailure("Title", "Title is required."),  // reported twice: shown once
            new ValidationFailure("Tasks[0].Title", "A task needs a title."),
            new ValidationFailure("Grateful[2]", "An entry must not be empty."),
            new ValidationFailure("habitId", "The habit to link does not exist.")
        };

        var (status, body, _) = await Handle(new ValidationException(failures));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        var errors = body.GetProperty("errors");
        Assert.Equal(["grateful[2]", "habitId", "tasks[0].title", "title"], errors.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["Title is required.", "Title is too long."], errors.GetProperty("title").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal("One or more validation errors occurred.", body.GetProperty("title").GetString());
    }
}

public class CurrentUserTests
{
    private static HannasHabits.Infrastructure.Services.CurrentUser For(HttpContext? context)
        => new(new HttpContextAccessor { HttpContext = context });

    private static DefaultHttpContext WithClaims(params Claim[] claims)
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };

    [Fact]
    public void TheIdComesFromTheNameIdentifierClaim()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, For(WithClaims(new Claim(ClaimTypes.NameIdentifier, id.ToString()))).UserId);
    }

    [Fact]
    public void ThenFromTheSubClaim_IfThereIsNoNameIdentifier()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, For(WithClaims(new Claim("sub", id.ToString()))).UserId);
    }

    [Fact]
    public void WithoutAUser_ItThrowsOnce_InsteadOfEveryHandlerCheckingForNull()
    {
        Assert.Throws<UnauthorizedAccessException>(() => For(null).UserId);
        Assert.Throws<UnauthorizedAccessException>(() => For(new DefaultHttpContext()).UserId);
        Assert.Throws<UnauthorizedAccessException>(() => For(WithClaims(new Claim("name", "Ada"))).UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-00000000000g")]
    public void AnIdThatIsNoGuid_IsNoUser(string id)
    {
        Assert.Throws<UnauthorizedAccessException>(() => For(WithClaims(new Claim(ClaimTypes.NameIdentifier, id))).UserId);
    }
}
