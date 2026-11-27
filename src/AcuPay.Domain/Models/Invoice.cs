namespace AcuPay.Domain.Models;

/// <summary>
/// An invoice together with the purchase order it points at, if any.
/// The PO fields are null when PoId is null.
/// </summary>
public sealed class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;

    public int VendorId { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;

    public int? PoId { get; set; }
    public string? PoNumber { get; set; }
    public decimal? PoAmount { get; set; }

    /// <summary>The vendor the PO belongs to. Compared against VendorId to spot a mismatch.</summary>
    public int? PoVendorId { get; set; }

    public decimal InvoiceAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public string? MatchNote { get; set; }
    public decimal? VariancePercent { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public string? DecidedBy { get; set; }
    public string? DecisionReason { get; set; }
}
