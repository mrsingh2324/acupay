using AcuPay.Domain.Exceptions;
using AcuPay.Domain.Models;
using AcuPay.Domain.Rules;
using Xunit;

namespace AcuPay.Tests;

public class ApprovalRulesTests
{
    private const int MinReasonLength = 10;

    private static ApprovalRules Rules() => new(MinReasonLength);

    private static Invoice Invoice(InvoiceStatus status, string createdBy = "requester1") => new()
    {
        InvoiceId = 1,
        InvoiceNumber = "INV-TEST",
        CreatedBy = createdBy,
        Status = status
    };

    [Fact]
    public void A_requester_cannot_decide()
    {
        Assert.Throws<ForbiddenRoleException>(() =>
            Rules().EnsureCanDecide(Invoice(InvoiceStatus.Matched), "requester1", "Requester", false, null));
    }

    [Fact]
    public void Nobody_can_decide_their_own_invoice()
    {
        // approver1 created INV-5003 in the seed data, which is why this case exists.
        Assert.Throws<SelfApprovalException>(() =>
            Rules().EnsureCanDecide(Invoice(InvoiceStatus.Exception, "approver1"), "approver1", "Approver", false, null));
    }

    [Fact]
    public void Self_approval_check_ignores_casing()
    {
        Assert.Throws<SelfApprovalException>(() =>
            Rules().EnsureCanDecide(Invoice(InvoiceStatus.Matched, "Approver1"), "approver1", "Approver", false, null));
    }

    [Theory]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    [InlineData(InvoiceStatus.Pending)]
    public void Only_matched_and_exception_invoices_can_be_decided(InvoiceStatus status)
    {
        Assert.Throws<InvalidStateException>(() =>
            Rules().EnsureCanDecide(Invoice(status), "approver1", "Approver", false, null));
    }

    [Fact]
    public void A_rejection_without_a_reason_is_refused()
    {
        Assert.Throws<ReasonRequiredException>(() =>
            Rules().EnsureCanDecide(Invoice(InvoiceStatus.Matched), "approver1", "Approver", true, "short"));
    }

    [Fact]
    public void A_rejection_with_a_long_enough_reason_is_allowed()
    {
        Rules().EnsureCanDecide(Invoice(InvoiceStatus.Matched), "approver1", "Approver", true, "Duplicate of INV-4000");
    }

    [Fact]
    public void An_approval_needs_no_reason()
    {
        Rules().EnsureCanDecide(Invoice(InvoiceStatus.Exception), "finance1", "Finance", false, null);
    }

    [Fact]
    public void Finance_may_decide_an_exception()
    {
        Rules().EnsureCanDecide(Invoice(InvoiceStatus.Exception), "finance1", "Finance", false, null);
    }
}
