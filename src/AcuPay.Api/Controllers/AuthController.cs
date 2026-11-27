using AcuPay.Api.Dtos;
using AcuPay.Api.Security;
using AcuPay.Domain.Exceptions;
using AcuPay.Domain.Interfaces;
using AcuPay.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcuPay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly JwtTokenService _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserRepository users, JwtTokenService tokens, ILogger<AuthController> logger)
    {
        _users = users;
        _tokens = tokens;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        AppUser? user = await _users.GetByUsernameAsync(request.Username, cancellationToken);

        // Same error whether the user is unknown or the password is wrong, so the response
        // does not tell an attacker which usernames exist.
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login for {Username}", request.Username);
            throw new InvalidCredentialsException();
        }

        (string token, DateTime expiresAtUtc) = _tokens.Create(user);
        _logger.LogInformation("{Username} signed in as {Role}", user.Username, user.Role);

        return Ok(new LoginResponse
        {
            Token = token,
            Role = user.Role,
            ExpiresAtUtc = expiresAtUtc
        });
    }
}
