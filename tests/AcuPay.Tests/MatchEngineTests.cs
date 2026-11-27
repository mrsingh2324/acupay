using AcuPay.Domain.Exceptions;
using AcuPay.Domain.Models;
using AcuPay.Domain.Rules;
using Xunit;

namespace AcuPay.Tests;

public class MatchEngineTests
{
    private const decimal Tolerance = 2.00m;
    private const decimal AutoApproveLimit = 50000.00m;

    private static MatchEngine Engine() => new(Tolerance, AutoApproveLimit);

    /// <summary>An invoice that points at a PO belonging to the same vendor.</summary>
    private static Invoice Invoice(decimal invoiceAmount, decimal? poAmount, int vendorId = 1, int? poVendorId = 1) =>
        new()
        {
            InvoiceId = 1,
            InvoiceNumber = "INV-TEST",
            VendorId = vendorId,
            PoId = poAmount is null ? null : 10,
            PoAmount = poAmount,
            PoVendorId = poAmount is null ? null : poVendorId,
            InvoiceAmount = invoiceAmount,
            Status = InvoiceStatus.Pending,
            CreatedBy = "requester1"
        };

    [Fact]
    public void Invoice_with_no_po_is_an_exception()
    {
        MatchResult result = Engine().Match(Invoice(12000m, poAmount: null));

        Assert.Equal(InvoiceStatus.Exception, result.Status);
        Assert.Equal(MatchNotes.NoPo, result.MatchNote);
        Assert.Null(result.VariancePercent);
    }

    [Fact]
    public void Po_belonging_to_another_vendor_is_an_exception()
    {
        MatchResult result = Engine().Match(Invoice(19000m, 20000m, vendorId: 1, poVendorId: 2));

        Assert.Equal(InvoiceStatus.Exception, result.Status);
        Assert.Equal(MatchNotes.VendorMismatch, result.MatchNote);
    }

    [Fact]
    public void Overbilling_beyond_tolerance_is_an_exception()
    {
        // INV-5003 in the seed data: 23000 against a 20000 PO is 15% over.
        MatchResult result = Engine().Match(Invoice(23000m, 20000m));

        Assert.Equal(InvoiceStatus.Exception, result.Status);
        Assert.Equal(MatchNotes.OverPoAmount, result.MatchNote);
        Assert.Equal(15.00m, result.VariancePercent);
    }

    [Fact]
    public void Matched_invoice_above_the_limit_waits_for_a_person()
    {
        // INV-5001: 1.5% over, but 101500 is above the auto-approve limit.
        MatchResult result = Engine().Match(Invoice(101500m, 100000m));

        Assert.Equal(InvoiceStatus.Matched, result.Status);
        Assert.Equal(MatchNotes.WithinTolerance, result.MatchNote);
        Assert.Equal(1.50m, result.VariancePercent);
        Assert.Null(result.DecidedBy);
    }

    [Fact]
    public void Matched_invoice_at_or_below_the_limit_is_auto_approved()
    {
        // INV-5002: exactly -2.00%, which is on the boundary and must match, and 49000 is under the limit.
        MatchResult result = Engine().Match(Invoice(49000m, 50000m));

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(MatchNotes.AutoApproved, result.MatchNote);
        Assert.Equal(-2.00m, result.VariancePercent);
        Assert.Equal(MatchResult.SystemUser, result.DecidedBy);
    }

    [Fact]
    public void An_invoice_exactly_on_the_limit_is_auto_approved()
    {
        MatchResult result = Engine().Match(Invoice(50000m, 50000m));

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(0.00m, result.VariancePercent);
    }

    [Fact]
    public void Underbilling_matches()
    {
        // INV-5006: 36000 against a 40000 PO is 10% under, which is acceptable.
        MatchResult result = Engine().Match(Invoice(36000m, 40000m));

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(-10.00m, result.VariancePercent);
    }

    [Theory]
    [InlineData(101500, 100000, 1.50)]
    [InlineData(23000, 20000, 15.00)]
    [InlineData(49000, 50000, -2.00)]
    [InlineData(100001, 100000, 0.00)]
    public void Variance_is_rounded_to_two_decimals(decimal invoiceAmount, decimal poAmount, decimal expected)
    {
        Assert.Equal(expected, MatchEngine.CalculateVariancePercent(invoiceAmount, poAmount));
    }

    [Fact]
    public void A_po_amount_of_zero_is_rejected_rather_than_dividing_by_zero()
    {
        Assert.Throws<InvalidPoAmountException>(() => MatchEngine.CalculateVariancePercent(1000m, 0m));
    }
}
