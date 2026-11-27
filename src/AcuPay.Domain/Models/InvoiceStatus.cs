namespace AcuPay.Domain.Models;

public enum InvoiceStatus
{
    Pending,
    Matched,
    Exception,
    Approved,
    Rejected
}
