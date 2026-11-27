using System.ComponentModel.DataAnnotations;

namespace AcuPay.Api.Dtos;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public required string Token { get; init; }
    public required string Role { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
