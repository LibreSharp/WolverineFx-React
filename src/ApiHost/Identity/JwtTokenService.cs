using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApiHost.Identity;

public class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JwtRegisteredClaimNames.Sub] = user.Id,
            [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
        };

        // One claim per role (a JSON array in the token), read by [Authorize(Roles = ...)].
        var roleNames = roles.ToArray();
        if (roleNames.Length > 0)
        {
            claims[ClaimTypes.Role] = roleNames;
        }

        var signingKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var handler = new JsonWebTokenHandler();
        var accessToken = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = claims,
            Expires = expiresAtUtc.UtcDateTime,
            SigningCredentials = credentials
        });

        return (accessToken, expiresAtUtc);
    }

    public static string GenerateRefreshTokenPlaintext()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public DateTimeOffset RefreshTokenExpiry() => DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenLifetimeDays);
}
