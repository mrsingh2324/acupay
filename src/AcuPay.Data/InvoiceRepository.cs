using AcuPay.Domain.Exceptions;
using AcuPay.Domain.Interfaces;
using AcuPay.Domain.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AcuPay.Data;

/// <summary>
/// Every method calls a stored procedure. There is no SQL text built in this file,
/// and every value reaches the server as a SqlParameter.
/// </summary>
public sealed class InvoiceRepository : IInvoiceRepository
{
    // SQL Server raises these two for a unique key violation.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    // RAISERROR with severity 16 from a procedure arrives as this number.
    private const int UserRaisedError = 50000;

    private readonly DbConnectionFactory _factory;

    public InvoiceRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync(string? status, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = await _factory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_GetInvoices", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value =
            string.IsNullOrWhiteSpace(status) ? DBNull.Value : status;

        var invoices = new List<Invoice>();

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            invoices.Add(ReadInvoice(reader));
        }

        return invoices;
    }

    public async Task<Invoice?> GetInvoiceByIdAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = await _factory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_GetInvoiceById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId;

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadInvoice(reader) : null;
    }

    public async Task<int> InsertInvoiceAsync(
        string invoiceNumber,
        string vendorCode,
        string? poNumber,
        decimal invoiceAmount,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = await _factory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_InsertInvoice", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@InvoiceNumber", SqlDbType.NVarChar, 20).Value = invoiceNumber;
        command.Parameters.Add("@VendorCode", SqlDbType.NVarChar, 10).Value = vendorCode;
        command.Parameters.Add("@PoNumber", SqlDbType.NVarChar, 20).Value = (object?)poNumber ?? DBNull.Value;
        SqlParameter amount = command.Parameters.Add("@InvoiceAmount", SqlDbType.Decimal);
        amount.Precision = 18;
        amount.Scale = 2;
        amount.Value = invoiceAmount;
        command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 50).Value = createdBy;

        SqlParameter idParameter = command.Parameters.Add("@InvoiceId", SqlDbType.Int);
        idParameter.Direction = ParameterDirection.Output;

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is UniqueIndexViolation or UniqueConstraintViolation)
        {
            // Turn a storage-level failure into a business failure the API can answer 409 to.
            throw new DuplicateInvoiceException(invoiceNumber);
        }
        catch (SqlException ex) when (ex.Number == UserRaisedError)
        {
            throw new UnknownReferenceException(
                ex.Message.Trim(),
                ex.Message.Trim() == "UNKNOWN_PO"
                    ? "That purchase order number does not exist."
                    : "That vendor code does not exist.");
        }

        return (int)idParameter.Value;
    }

    public async Task UpdateInvoiceMatchAsync(int invoiceId, MatchResult result, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = await _factory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_UpdateInvoiceMatch", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = result.Status.ToString();
        command.Parameters.Add("@MatchNote", SqlDbType.NVarChar, 50).Value = result.MatchNote;
        SqlParameter variance = command.Parameters.Add("@VariancePercent", SqlDbType.Decimal);
        variance.Precision = 9;
        variance.Scale = 2;
        variance.Value = (object?)result.VariancePercent ?? DBNull.Value;
        command.Parameters.Add("@DecidedBy", SqlDbType.NVarChar, 50).Value =
            (object?)result.DecidedBy ?? DBNull.Value;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DecideInvoiceAsync(
        int invoiceId,
        InvoiceStatus status,
        string decidedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = await _factory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_DecideInvoice", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@InvoiceId", SqlDbType.Int).Value = invoiceId;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status.ToString();
        command.Parameters.Add("@DecidedBy", SqlDbType.NVarChar, 50).Value = decidedBy;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 200).Value = (object?)reason ?? DBNull.Value;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using SqlConnection connection = await _factory.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private static Invoice ReadInvoice(SqlDataReader reader) => new()
    {
        InvoiceId = reader.GetInt32(reader.GetOrdinal("InvoiceId")),
        InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
        VendorId = reader.GetInt32(reader.GetOrdinal("VendorId")),
        VendorCode = reader.GetString(reader.GetOrdinal("VendorCode")),
        VendorName = reader.GetString(reader.GetOrdinal("VendorName")),
        PoId = GetNullableInt(reader, "PoId"),
        PoNumber = GetNullableString(reader, "PoNumber"),
        PoAmount = GetNullableDecimal(reader, "PoAmount"),
        PoVendorId = GetNullableInt(reader, "PoVendorId"),
        InvoiceAmount = reader.GetDecimal(reader.GetOrdinal("InvoiceAmount")),
        Status = Enum.Parse<InvoiceStatus>(reader.GetString(reader.GetOrdinal("Status"))),
        MatchNote = GetNullableString(reader, "MatchNote"),
        VariancePercent = GetNullableDecimal(reader, "VariancePercent"),
        CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),
        DecidedBy = GetNullableString(reader, "DecidedBy"),
        DecisionReason = GetNullableString(reader, "DecisionReason")
    };

    private static int? GetNullableInt(SqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static decimal? GetNullableDecimal(SqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static string? GetNullableString(SqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
