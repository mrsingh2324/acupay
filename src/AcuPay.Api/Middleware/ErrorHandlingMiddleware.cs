using AcuPay.Api.Dtos;
using AcuPay.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace AcuPay.Api.Middleware;

/// <summary>
/// The only place an exception turns into a response. Controllers contain no try/catch,
/// and no stack trace ever reaches the caller.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // Expected: a rule said no. Log at warning, answer with the rule's own code.
            _logger.LogWarning("Rule refused the request: {Code} {Message}", ex.Code, ex.Message);
            await WriteAsync(context, StatusFor(ex), ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected: something is broken. Log everything, tell the caller nothing.
            _logger.LogError(ex, "Unhandled exception while handling {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteAsync(context, HttpStatusCode.InternalServerError, "INTERNAL_ERROR",
                "Something went wrong. Quote the correlation id when you report this.");
        }
    }

    private static HttpStatusCode StatusFor(DomainException ex) => ex switch
    {
        InvoiceNotFoundException   => HttpStatusCode.NotFound,
        ForbiddenRoleException     => HttpStatusCode.Forbidden,
        SelfApprovalException      => HttpStatusCode.Forbidden,
        InvalidStateException      => HttpStatusCode.Conflict,
        DuplicateInvoiceException  => HttpStatusCode.Conflict,
        InvalidCredentialsException=> HttpStatusCode.Unauthorized,
        ReasonRequiredException    => HttpStatusCode.BadRequest,
        InvalidPoAmountException   => HttpStatusCode.BadRequest,
        UnknownReferenceException  => HttpStatusCode.BadRequest,
        _                          => HttpStatusCode.BadRequest
    };

    public static async Task WriteAsync(HttpContext context, HttpStatusCode status, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";

        var payload = new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = code,
                Message = message,
                CorrelationId = context.CorrelationId()
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
