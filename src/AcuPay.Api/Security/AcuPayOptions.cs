namespace AcuPay.Api.Security;

/// <summary>Bound from the AcuPay section of appsettings.json.</summary>
public sealed class AcuPayOptions
{
    public const string SectionName = "AcuPay";

    public decimal PriceTolerancePercent { get; set; } = 2.00m;
    public decimal AutoApproveLimit { get; set; } = 50000.00m;
    public int MinReasonLength { get; set; } = 10;
    public int TokenExpiryMinutes { get; set; } = 60;
}
