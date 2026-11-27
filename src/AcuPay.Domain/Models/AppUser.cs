namespace AcuPay.Domain.Models;

public sealed class AppUser
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
