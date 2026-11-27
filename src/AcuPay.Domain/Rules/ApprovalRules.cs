using AcuPay.Domain.Exceptions;
using AcuPay.Domain.Models;

namespace AcuPay.Domain.Rules;

/// <summary>
/// The four checks that decide whether this user may approve or reject this invoice.
/// Each one throws its own exception type, which the API middleware turns into a status code.
/// </summary>
public sealed class ApprovalRules
{
    public const string ApproverRole = "Approver";
    public const string FinanceRole = "Finance";

    private readonly int _minReasonLength;

    public ApprovalRules(int minReasonLength) => _minReasonLength = minReasonLength;

    public void EnsureCanDecide(Invoice invoice, string username, string role, bool isRejection, string? reason)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (!IsDecider(role))
        {
            throw new ForbiddenRoleException();
        }

        // The username comes from the token claim. Never from the request body.
        if (string.Equals(invoice.CreatedBy, username, StringComparison.OrdinalIgnoreCase))
        {
            throw new SelfApprovalException();
        }

        if (invoice.Status is not (InvoiceStatus.Matched or InvoiceStatus.Exception))
        {
            throw new InvalidStateException(invoice.Status.ToString());
        }

        if (isRejection && (reason is null || reason.Trim().Length < _minReasonLength))
        {
            throw new ReasonRequiredException(_minReasonLength);
        }
    }

    private static bool IsDecider(string role) =>
        role is ApproverRole or FinanceRole;
}
