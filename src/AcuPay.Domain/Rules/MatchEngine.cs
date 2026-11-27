using AcuPay.Domain.Exceptions;
using AcuPay.Domain.Models;

namespace AcuPay.Domain.Rules;

/// <summary>
/// The four matching rules and auto-approval. No database, no HTTP, no logging.
/// Everything it needs arrives through the constructor or the method argument,
/// which is what makes it straightforward to unit test.
/// </summary>
public sealed class MatchEngine
{
    private readonly decimal _tolerancePercent;
    private readonly decimal _autoApproveLimit;

    public MatchEngine(decimal tolerancePercent, decimal autoApproveLimit)
    {
        _tolerancePercent = tolerancePercent;
        _autoApproveLimit = autoApproveLimit;
    }

    /// <summary>
    /// Rules run in order and the first one that applies wins:
    /// 1 no PO, 2 vendor mismatch, 3 over tolerance, 4 everything else matches.
    /// A matched invoice at or below the auto-approve limit is approved straight away.
    /// </summary>
    public MatchResult Match(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (invoice.PoId is null || invoice.PoAmount is null)
        {
            return MatchResult.Exception(MatchNotes.NoPo, null);
        }

        if (invoice.PoVendorId != invoice.VendorId)
        {
            return MatchResult.Exception(MatchNotes.VendorMismatch, null);
        }

        decimal variance = CalculateVariancePercent(invoice.InvoiceAmount, invoice.PoAmount.Value);

        if (variance > _tolerancePercent)
        {
            return MatchResult.Exception(MatchNotes.OverPoAmount, variance);
        }

        // Billing less than the PO is acceptable, so a negative variance matches.
        return invoice.InvoiceAmount <= _autoApproveLimit
            ? MatchResult.AutoApproved(variance)
            : MatchResult.Matched(variance);
    }

    /// <summary>(invoice - po) / po * 100, rounded to two decimals before any comparison.</summary>
    public static decimal CalculateVariancePercent(decimal invoiceAmount, decimal poAmount)
    {
        if (poAmount <= 0m)
        {
            throw new InvalidPoAmountException();
        }

        decimal raw = (invoiceAmount - poAmount) / poAmount * 100m;
        return Math.Round(raw, 2, MidpointRounding.AwayFromZero);
    }
}
