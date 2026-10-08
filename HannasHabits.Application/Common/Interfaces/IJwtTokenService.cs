namespace HannasHabits.Application.Common.Interfaces;

public record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, DateTime RefreshTokenExpiresAt);

public interface IJwtTokenService
{
    Task<TokenPair> CreateTokenPairAsync(IdentityUserDto user, string? ipAddress = null);
    Task<TokenPair?> RefreshAsync(string refreshToken, string? ipAddress = null);
    Task<bool> RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null);
    Task RevokeAllForUserAsync(Guid userId);
}