using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PaymentService.Controllers;

[ApiController]
[Route("")]
public class OperationController : ControllerBase
{
    private readonly IOperationService _service;
    private readonly ILogger<OperationController> _logger;

    public OperationController(IOperationService service, ILogger<OperationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new payment operation.
    /// </summary>
    /// <remarks>Returns 409 Conflict if an operation with the same OperationId already exists.</remarks>
    [HttpPost("operations")]
    [ProducesResponseType(typeof(OperationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OperationResponse>> CreateOperation([FromBody] CreateOperationRequest request, CancellationToken ct)
    {
        var result = await _service.CreateOperationAsync(request, ct);
        return CreatedAtAction(nameof(GetOperation), new { id = result.Id }, result);
    }

    /// <summary>
    /// Submits the operation to the provider.
    /// </summary>
    /// <remarks>
    /// Returns 202 Accepted on the first call. 
    /// Returns 200 OK with the current operation state on subsequent calls (idempotent).
    /// </remarks>
    [HttpPost("operations/{id}/submit")]
    [ProducesResponseType(typeof(OperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitOperation(string id, CancellationToken ct)
    {
        var (operation, isFirstSubmission) = await _service.SubmitOperationAsync(id, ct);

        if (isFirstSubmission)
        {
            return Accepted();
        }

        return Ok(new OperationResponse
        {
            Id = operation.Id.ToString(),
            Status = operation.Status.ToString(),
            ProviderPaymentId = operation.ProviderPaymentId,
            CreatedAt = operation.CreatedAt
        });
    }

    /// <summary>
    /// Processes a receipt callback from the payment provider.
    /// </summary>
    /// <remarks>
    /// Returns 204 No Content on success. 
    /// Returns 409 Conflict if the ProviderPaymentId does not match the existing one.
    /// </remarks>
    [HttpPost("receipts")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ProcessReceipt([FromBody] ReceiptRequest request, CancellationToken ct)
    {
        await _service.ProcessReceiptAsync(request, ct);
        return NoContent();
    }

    /// <summary>
    /// Retrieves operation details by its OperationId.
    /// </summary>
    [HttpGet("operations/{id}")]
    [ProducesResponseType(typeof(OperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationResponse>> GetOperation(string id, CancellationToken ct)
    {
        var result = await _service.GetOperationAsync(id, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the event history (audit log) for a specific operation.
    /// </summary>
    [HttpGet("operations/{id}/events")]
    [ProducesResponseType(typeof(IReadOnlyList<OperationEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OperationEventResponse>>> GetOperationEvents(string id, CancellationToken ct)
    {
        var result = await _service.GetOperationEventsAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Health check endpoint for orchestrators (Docker/K8s) and monitoring.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok();
    }
}