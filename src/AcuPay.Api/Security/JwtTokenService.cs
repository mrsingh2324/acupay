using AcuPay.Domain.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AcuPay.Api.Security;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly AcuPayOptions _options;

    public JwtTokenService(IConfiguration configuration, IOptions<AcuPayOptions> options)
    {
        _configuration = configuration;
        _options = options.Value;
    }

    /// <summary>
    /// Two claims and nothing else: who they are and what they may do.
    /// Everything the API decides about permissions comes from this token.
    /// </summary>
    public (string Token, DateTime ExpiresAtUtc) Create(AppUser user)
    {
        string key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is not configured. Run: dotnet user-secrets set \"Jwt:Key\" \"<32+ random characters>\"");

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        ];

        DateTime expires = DateTime.UtcNow.AddMinutes(_options.TokenExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
