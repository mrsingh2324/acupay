using AcuPay.Domain.Interfaces;
using AcuPay.Domain.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AcuPay.Data;

public sealed class UserRepository : IUserRepository
{
    private readonly DbConnectionFactory _factory;

    public UserRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = await _factory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_GetUserByUsername", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AppUser
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            Role = reader.GetString(reader.GetOrdinal("Role"))
        };
    }
}
