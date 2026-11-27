using AcuPay.Domain.Models;

namespace AcuPay.Domain.Interfaces;

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> GetInvoicesAsync(string? status, CancellationToken cancellationToken = default);

    Task<Invoice?> GetInvoiceByIdAsync(int invoiceId, CancellationToken cancellationToken = default);

    Task<int> InsertInvoiceAsync(
        string invoiceNumber,
        string vendorCode,
        string? poNumber,
        decimal invoiceAmount,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task UpdateInvoiceMatchAsync(
        int invoiceId,
        MatchResult result,
        CancellationToken cancellationToken = default);

    Task DecideInvoiceAsync(
        int invoiceId,
        InvoiceStatus status,
        string decidedBy,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
