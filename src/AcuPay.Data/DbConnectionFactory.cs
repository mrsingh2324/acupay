using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AcuPay.Data;

/// <summary>
/// Hands out connections. The connection string is read from configuration and nowhere else:
/// user-secrets locally, a GitHub Actions secret in CI, environment variables in a deployment.
/// There is deliberately no fallback default here. If it is missing, fail loudly at startup.
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("AcuPay")
            ?? throw new InvalidOperationException(
                "Connection string 'AcuPay' is not configured. Run: dotnet user-secrets set \"ConnectionStrings:AcuPay\" \"<your connection string>\"");
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
