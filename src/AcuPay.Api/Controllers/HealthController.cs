using AcuPay.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcuPay.Api.Controllers;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly IInvoiceRepository _invoices;
    private readonly IWebHostEnvironment _environment;

    public HealthController(IInvoiceRepository invoices, IWebHostEnvironment environment)
    {
        _invoices = invoices;
        _environment = environment;
    }

    /// <summary>
    /// 200 when the database answers, 503 when it does not. This is the first thing
    /// to check during an incident, so it must never need a token.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        bool databaseUp = await _invoices.CanConnectAsync(cancellationToken);

        var body = new
        {
            status = databaseUp ? "Healthy" : "Unhealthy",
            environment = _environment.EnvironmentName,
            database = databaseUp ? "Reachable" : "Unreachable",
            checkedAtUtc = DateTime.UtcNow
        };

        return databaseUp ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
