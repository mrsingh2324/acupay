using AcuPay.Domain.Models;

namespace AcuPay.Domain.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
