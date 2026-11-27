using AcuPay.Api.Dtos;
using AcuPay.Domain.Exceptions;
using AcuPay.Domain.Interfaces;
using AcuPay.Domain.Models;
using AcuPay.Domain.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AcuPay.Api.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceRepository _invoices;
    private readonly MatchEngine _matchEngine;
    private readonly ApprovalRules _approvalRules;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        IInvoiceRepository invoices,
        MatchEngine matchEngine,
        ApprovalRules approvalRules,
        ILogger<InvoicesController> logger)
    {
        _invoices = invoices;
        _matchEngine = matchEngine;
        _approvalRules = approvalRules;
        _logger = logger;
    }

    /// <summary>The caller's identity comes from the token. Never from the body or a header.</summary>
    private string CurrentUser =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? throw new InvalidCredentialsException();

    private string CurrentRole =>
        User.FindFirstValue(ClaimTypes.Role)
        ?? throw new ForbiddenRoleException();

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InvoiceResponse>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Invoice> invoices = await _invoices.GetInvoicesAsync(status, cancellationToken);
        return Ok(invoices.Select(i => i.ToResponse()));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        Invoice invoice = await LoadAsync(id, cancellationToken);
        return Ok(invoice.ToResponse());
    }

    [HttpPost]
    [Authorize(Roles = "Requester,Finance")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        int id = await _invoices.InsertInvoiceAsync(
            request.InvoiceNumber,
            request.VendorCode,
            request.PoNumber,
            request.InvoiceAmount,
            CurrentUser,
            cancellationToken);

        _logger.LogInformation("{User} created invoice {InvoiceId} ({InvoiceNumber})",
            CurrentUser, id, request.InvoiceNumber);

        Invoice created = await LoadAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, created.ToResponse());
    }

    [HttpPost("{id:int}/match")]
    [Authorize(Roles = "Requester,Finance")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceResponse>> Match(int id, CancellationToken cancellationToken)
    {
        Invoice invoice = await LoadAsync(id, cancellationToken);

        MatchResult result = _matchEngine.Match(invoice);
        await _invoices.UpdateInvoiceMatchAsync(id, result, cancellationToken);

        _logger.LogInformation("Invoice {InvoiceId} matched as {Status} ({Note})",
            id, result.Status, result.MatchNote);

        Invoice updated = await LoadAsync(id, cancellationToken);
        return Ok(updated.ToResponse());
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Approver,Finance")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public Task<ActionResult<InvoiceResponse>> Approve(
        int id,
        DecisionRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(id, InvoiceStatus.Approved, request.Reason, isRejection: false, cancellationToken);

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Approver,Finance")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<InvoiceResponse>> Reject(
        int id,
        DecisionRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(id, InvoiceStatus.Rejected, request.Reason, isRejection: true, cancellationToken);

    private async Task<ActionResult<InvoiceResponse>> DecideAsync(
        int id,
        InvoiceStatus status,
        string? reason,
        bool isRejection,
        CancellationToken cancellationToken)
    {
        Invoice invoice = await LoadAsync(id, cancellationToken);

        // Throws if the role is wrong, the user created it, the status is final,
        // or a rejection arrived without a long enough reason.
        _approvalRules.EnsureCanDecide(invoice, CurrentUser, CurrentRole, isRejection, reason);

        await _invoices.DecideInvoiceAsync(id, status, CurrentUser, reason, cancellationToken);

        _logger.LogInformation("Invoice {InvoiceId} {Status} by {User}", id, status, CurrentUser);

        Invoice updated = await LoadAsync(id, cancellationToken);
        return Ok(updated.ToResponse());
    }

    private async Task<Invoice> LoadAsync(int id, CancellationToken cancellationToken) =>
        await _invoices.GetInvoiceByIdAsync(id, cancellationToken)
        ?? throw new InvoiceNotFoundException(id);
}
