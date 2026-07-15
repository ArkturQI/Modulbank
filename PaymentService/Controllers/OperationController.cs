using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace PaymentService.Controllers;

[ApiController]
[Route("")]
public class OperationController : ControllerBase
{
    private readonly IOperationService _service;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OperationController> _logger;

    public OperationController(
        IOperationService service,
        IHttpClientFactory httpClientFactory,
        ILogger<OperationController> logger)
    {
        _service = service;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new payment operation
    /// </summary>
    [HttpPost("operations")]
    [ProducesResponseType(typeof(OperationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationResponse>> CreateOperation(
        [FromBody] CreateOperationRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating operation with id: {OperationId}", request.OperationId);

        var result = await _service.CreateOperationAsync(request, ct);

        _logger.LogInformation("Operation created: {Id}", result.Id);

        return CreatedAtAction(nameof(GetOperation), new { id = result.Id }, result);
    }

    /// <summary>
    /// Submits operation to provider (idempotent)
    /// </summary>
    [HttpPost("operations/{id}/submit")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitOperation(string id, CancellationToken ct)
    {
        _logger.LogInformation("Submitting operation: {OperationId}", id);

        await _service.SubmitOperationAsync(id, ct);

        // Send request to provider with idempotency headers
        var client = _httpClientFactory.CreateClient("ProviderClient");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/payments");

        requestMessage.Headers.Add("Idempotency-Key", id);
        requestMessage.Headers.Add("X-Correlation-ID", id);

        requestMessage.Content = new StringContent(
            $"{{\"operationId\": \"{id}\"}}",
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(requestMessage, ct);

        // Log provider response but don't fail - status will be updated via callback
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Provider returned {StatusCode} for operation {OperationId}",
                response.StatusCode,
                id);
        }
        else
        {
            _logger.LogInformation("Provider accepted operation: {OperationId}", id);
        }

        return Accepted();
    }

    /// <summary>
    /// Processes receipt callback from provider
    /// </summary>
    [HttpPost("receipts")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ProcessReceipt(
        [FromBody] ReceiptRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing receipt for operation: {OperationId}, provider: {ProviderId}, result: {Result}",
            request.OperationId,
            request.ProviderPaymentId,
            request.Result);

        await _service.ProcessReceiptAsync(request, ct);

        _logger.LogInformation("Receipt processed successfully: {OperationId}", request.OperationId);

        return NoContent();
    }

    /// <summary>
    /// Gets operation details by id
    /// </summary>
    [HttpGet("operations/{id}")]
    [ProducesResponseType(typeof(OperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationResponse>> GetOperation(string id, CancellationToken ct)
    {
        _logger.LogDebug("Getting operation: {OperationId}", id);

        var result = await _service.GetOperationAsync(id, ct);
        if (result == null)
        {
            _logger.LogWarning("Operation not found: {OperationId}", id);
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets operation event history
    /// </summary>
    [HttpGet("operations/{id}/events")]
    [ProducesResponseType(typeof(IReadOnlyList<OperationEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OperationEventResponse>>> GetOperationEvents(string id, CancellationToken ct)
    {
        _logger.LogDebug("Getting events for operation: {OperationId}", id);

        var result = await _service.GetOperationEventsAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok();
    }
}