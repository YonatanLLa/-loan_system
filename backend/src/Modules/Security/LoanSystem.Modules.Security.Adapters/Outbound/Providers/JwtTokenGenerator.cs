using System.Security.Claims;
using System.Text;
using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

/// <summary>
/// Issues HMAC-SHA256 (HS256) access tokens whose issuer, audience and claim types match the
/// validation configured in the WebApi host. Claim types align with <c>CurrentUserProvider</c> (BE-SEC-01).
/// </summary>
internal sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private const int DefaultAccessTokenMinutes = 30;

    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;

    public JwtTokenGenerator(IConfiguration configuration, IClock clock)
    {
        _clock = clock;
        var key = configuration.GetRequiredValue("Jwt:Key");
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        _issuer = configuration["Jwt:Issuer"] ?? "LoanSystem.Identity";
        _audience = configuration["Jwt:Audience"] ?? "LoanSystem.App";
        _accessTokenMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var minutes) && minutes > 0
            ? minutes
            : DefaultAccessTokenMinutes;
    }

    public (string AccessToken, DateTimeOffset ExpiresAt) Generate(User user)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_accessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [ClaimTypes.NameIdentifier] = user.Id.ToString(),
                [ClaimTypes.Email] = user.Email,
                [ClaimTypes.Role] = user.Role.ToString()
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAt);
    }
}
