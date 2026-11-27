namespace AcuPay.Domain.Models;

/// <summary>
/// What the match engine decided. Immutable, built through the factory methods
/// so an invalid combination cannot be constructed.
/// </summary>
public sealed class MatchResult
{
    public const string SystemUser = "SYSTEM";

    private MatchResult(InvoiceStatus status, string matchNote, decimal? variancePercent, string? decidedBy)
    {
        Status = status;
        MatchNote = matchNote;
        VariancePercent = variancePercent;
        DecidedBy = decidedBy;
    }

    public InvoiceStatus Status { get; }
    public string MatchNote { get; }
    public decimal? VariancePercent { get; }

    /// <summary>SYSTEM when the invoice was auto-approved, otherwise null.</summary>
    public string? DecidedBy { get; }

    public static MatchResult Exception(string note, decimal? variancePercent) =>
        new(InvoiceStatus.Exception, note, variancePercent, null);

    public static MatchResult Matched(decimal variancePercent) =>
        new(InvoiceStatus.Matched, MatchNotes.WithinTolerance, variancePercent, null);

    public static MatchResult AutoApproved(decimal variancePercent) =>
        new(InvoiceStatus.Approved, MatchNotes.AutoApproved, variancePercent, SystemUser);
}

public static class MatchNotes
{
    public const string NoPo = "NO_PO";
    public const string VendorMismatch = "VENDOR_MISMATCH";
    public const string OverPoAmount = "OVER_PO_AMOUNT";
    public const string WithinTolerance = "WITHIN_TOLERANCE";
    public const string AutoApproved = "AUTO_APPROVED";
}
