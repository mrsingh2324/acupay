namespace AcuPay.Api.Dtos;

/// <summary>The one error shape every endpoint returns, for every failure.</summary>
public sealed class ApiErrorResponse
{
    public required ApiError Error { get; init; }
}

public sealed class ApiError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string CorrelationId { get; init; }
}
