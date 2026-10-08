using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HannasHabits.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeSpan _accessTokenLifetime;
    private readonly TimeSpan _refreshTokenLifetime;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _secretKey;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;

        var jwt = options.Value;
        _accessTokenLifetime = TimeSpan.FromMinutes(jwt.AccessMinutes);
        _refreshTokenLifetime = TimeSpan.FromDays(jwt.RefreshDays);
        _issuer = jwt.Issuer;
        _audience = jwt.Audience;
        _secretKey = jwt.Key;
    }

    private string CreateAccessToken(ApplicationUser user, out DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? ""),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        expires = DateTime.UtcNow.Add(_accessTokenLifetime);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public async Task<TokenPair> CreateTokenPairAsync(IdentityUserDto dto, string? ipAddress = null)
    {
        var user = await _userManager.FindByIdAsync(dto.Id.ToString()) ?? throw new InvalidOperationException("User not found");

        var accessToken = CreateAccessToken(user, out var accessExp);
        var refreshToken = CreateRefreshToken();
        var refreshExp = DateTime.UtcNow.Add(_refreshTokenLifetime);

        var rt = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = refreshExp,
            CreatedAt = DateTime.UtcNow,
        };

        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();

        return new TokenPair(accessToken, refreshToken, accessExp, refreshExp);
    }

    public async Task<TokenPair?> RefreshAsync(string refreshToken, string? ipAddress = null)
    {
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
        if (rt == null || rt.IsRevoked || rt.ExpiresAt <= DateTime.UtcNow)
            return null;
        
        var user = await _userManager.FindByIdAsync(rt.UserId.ToString());
        if (user == null) return null;
        
        rt.IsRevoked = true;
        rt.ReplacedByToken = CreateRefreshToken();
        _db.RefreshTokens.Update(rt);
        
        var accessToken = CreateAccessToken(user, out var accessExp);
        var refreshTokenNew = CreateRefreshToken();
        var refreshExp = DateTime.UtcNow.Add(_refreshTokenLifetime);

        var newRt = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenNew,
            ExpiresAt = refreshExp,
            CreatedAt = DateTime.UtcNow,
            ReplacedByToken = null
        };

        _db.RefreshTokens.Add(newRt);
        await _db.SaveChangesAsync();

        return new TokenPair(accessToken, refreshTokenNew, accessExp, refreshExp);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
        if (rt == null || rt.IsRevoked) return false;

        rt.IsRevoked = true;
        _db.RefreshTokens.Update(rt);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        var tokens = await _db.RefreshTokens.Where(r => r.UserId == userId && !r.IsRevoked).ToListAsync();
        foreach (var t in tokens) t.IsRevoked = true;
        _db.RefreshTokens.UpdateRange(tokens);
        await _db.SaveChangesAsync();
    }
}