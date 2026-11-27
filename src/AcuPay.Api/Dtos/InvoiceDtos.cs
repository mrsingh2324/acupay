using System.ComponentModel.DataAnnotations;

namespace AcuPay.Api.Dtos;

public sealed class CreateInvoiceRequest
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(10, MinimumLength = 2)]
    public string VendorCode { get; set; } = string.Empty;

    /// <summary>Optional. An invoice with no PO becomes an exception when it is matched.</summary>
    [StringLength(20)]
    public string? PoNumber { get; set; }

    [Range(0.01, 99999999.99, ErrorMessage = "InvoiceAmount must be greater than zero.")]
    public decimal InvoiceAmount { get; set; }
}

public sealed class DecisionRequest
{
    /// <summary>Required on reject. Optional on approve.</summary>
    [StringLength(200)]
    public string? Reason { get; set; }
}

public sealed class InvoiceResponse
{
    public int InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string VendorCode { get; init; } = string.Empty;
    public string VendorName { get; init; } = string.Empty;
    public string? PoNumber { get; init; }
    public decimal? PoAmount { get; init; }
    public decimal InvoiceAmount { get; init; }
    public decimal? VariancePercent { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? MatchNote { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? DecidedBy { get; init; }
    public string? DecisionReason { get; init; }
}
