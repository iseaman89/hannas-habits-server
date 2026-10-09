namespace HannasHabits.Application.Auth;

public record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, DateTime RefreshTokenExpiresAt);

/// <summary>
/// The one response shape of register, login, Google login and refresh, so a client has a single code path
/// (and after a page reload a refresh is enough to get the user back).
/// </summary>
public record AuthResult(IdentityUserDto User, TokenPair Tokens);
