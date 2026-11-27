using AcuPay.Domain.Models;

namespace AcuPay.Api.Dtos;

/// <summary>
/// Domain entities never leave the API layer. This is the only place they turn into a response.
/// </summary>
public static class InvoiceMapper
{
    public static InvoiceResponse ToResponse(this Invoice invoice) => new()
    {
        InvoiceId = invoice.InvoiceId,
        InvoiceNumber = invoice.InvoiceNumber,
        VendorCode = invoice.VendorCode,
        VendorName = invoice.VendorName,
        PoNumber = invoice.PoNumber,
        PoAmount = invoice.PoAmount,
        InvoiceAmount = invoice.InvoiceAmount,
        VariancePercent = invoice.VariancePercent,
        Status = invoice.Status.ToString(),
        MatchNote = invoice.MatchNote,
        CreatedBy = invoice.CreatedBy,
        DecidedBy = invoice.DecidedBy,
        DecisionReason = invoice.DecisionReason
    };
}
