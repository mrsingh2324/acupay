namespace AcuPay.Domain.Exceptions;

/// <summary>
/// Base for every rule violation the application raises on purpose.
/// Code is the machine-readable string the API returns. The HTTP status is decided
/// in the API layer, so the domain stays free of HTTP concerns.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}

public sealed class InvoiceNotFoundException : DomainException
{
    public InvoiceNotFoundException(int invoiceId)
        : base("INVOICE_NOT_FOUND", $"Invoice {invoiceId} does not exist.") { }
}

public sealed class ForbiddenRoleException : DomainException
{
    public ForbiddenRoleException()
        : base("FORBIDDEN_ROLE", "Your role is not allowed to perform this action.") { }
}

public sealed class SelfApprovalException : DomainException
{
    public SelfApprovalException()
        : base("SELF_APPROVAL_BLOCKED", "You cannot decide an invoice you created.") { }
}

public sealed class InvalidStateException : DomainException
{
    public InvalidStateException(string currentStatus)
        : base("INVALID_STATE", $"An invoice in status {currentStatus} can no longer be decided.") { }
}

public sealed class ReasonRequiredException : DomainException
{
    public ReasonRequiredException(int minLength)
        : base("REASON_REQUIRED", $"A reason of at least {minLength} characters is required.") { }
}

public sealed class InvalidPoAmountException : DomainException
{
    public InvalidPoAmountException()
        : base("INVALID_PO_AMOUNT", "The purchase order amount must be greater than zero.") { }
}

public sealed class DuplicateInvoiceException : DomainException
{
    public DuplicateInvoiceException(string invoiceNumber)
        : base("DUPLICATE_INVOICE", $"Invoice {invoiceNumber} already exists for this vendor.") { }
}

public sealed class UnknownReferenceException : DomainException
{
    public UnknownReferenceException(string code, string message) : base(code, message) { }
}

public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("INVALID_CREDENTIALS", "The username or password is incorrect.") { }
}
