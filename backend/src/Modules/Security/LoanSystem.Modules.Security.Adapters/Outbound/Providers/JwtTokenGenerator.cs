using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.Extensions.Configuration;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

/// <summary>
/// Issues RS256-signed short-lived JWT access tokens with a deterministic <c>kid</c> header
/// sourced from <see cref="IJwtKeyRing"/> (BE-SEC-01, BE-SEC-10).
/// </summary>
internal sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private const int DefaultAccessTokenMinutes = 30;

    private readonly IJwtKeyRing _keyRing;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public JwtTokenGenerator(
        IJwtKeyRing keyRing,
        IConfiguration configuration,
        IClock clock)
    {
        _keyRing = keyRing;
        _configuration = configuration;
        _clock = clock;
    }

    public (string AccessToken, DateTimeOffset ExpiresAt) Generate(User user)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "LoanSystem.Identity";
        var audience = _configuration["Jwt:Audience"] ?? "LoanSystem.App";
        var minutes = int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var parsedMinutes) && parsedMinutes > 0
            ? parsedMinutes
            : DefaultAccessTokenMinutes;

        var credentials = _keyRing.GetSigningCredentials();
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(minutes);

        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return (encoded, expiresAt);
    }
}
