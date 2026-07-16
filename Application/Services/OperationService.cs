using System.Globalization;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class OperationService : IOperationService
{
    private readonly IOperationRepository _repository;

    public OperationService(IOperationRepository repository)
    {
        _repository = repository;
    }

    public async Task<OperationResponse> CreateOperationAsync(CreateOperationRequest request, CancellationToken ct = default)
    {
        var operationId = request.OperationId ?? Guid.NewGuid().ToString();

        var existing = await _repository.GetByIdAsync(operationId, ct);
        if (existing != null)
        {
            throw new ConflictException($"Operation with id {operationId} already exists");
        }

        if (!decimal.TryParse(request.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalAmount))
        {
            throw new ArgumentException("Invalid amount format");
        }

        // Domain-driven design: encapsulate creation logic and invariants within the domain entity
        var operation = Operation.Create(operationId, decimalAmount, request.Currency, request.Description);
        await _repository.AddAsync(operation, ct);

        return MapToResponse(operation);
    }

    public async Task<(Operation Operation, bool IsFirstSubmission)> SubmitOperationAsync(string operationId, CancellationToken ct = default)
    {
        var operation = await _repository.GetByIdAsync(operationId, ct)
            ?? throw new NotFoundException($"Operation {operationId} not found");

        if (operation.Status is OperationStatus.Completed or OperationStatus.Rejected or OperationStatus.Processing)
        {
            return (operation, false);
        }

        operation.ChangeStatus(OperationStatus.Processing, "Submit called");

        try
        {
            await _repository.SaveChangesAsync(ct);
            return (operation, true);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Handle race conditions: if a concurrent request already transitioned the status, 
            // fetch the latest state to return an accurate result without failing the request.
            var updatedOperation = await _repository.GetByIdAsync(operationId, ct)
                ?? throw new NotFoundException($"Operation {operationId} not found");

            return (updatedOperation, false);
        }
    }

    public async Task ProcessReceiptAsync(ReceiptRequest request, CancellationToken ct = default)
    {
        var operation = await _repository.GetByIdAsync(request.OperationId, ct)
            ?? throw new NotFoundException($"Operation {request.OperationId} not found");

        if (!operation.TrySetProviderPaymentId(request.ProviderPaymentId))
        {
            throw new ConflictException($"ProviderPaymentId mismatch. Existing: {operation.ProviderPaymentId}, New: {request.ProviderPaymentId}");
        }

        if (operation.Status is OperationStatus.Completed or OperationStatus.Rejected)
        {
            // Idempotency & Audit: safely ignore late receipts for terminal states, 
            // but still record them in the event history for debugging and auditability.
            operation.RecordIgnoredReceipt(request.Result);
            await _repository.SaveChangesAsync(ct);
            return;
        }

        var newStatus = request.Result.ToUpper() switch
        {
            "COMPLETED" => OperationStatus.Completed,
            "REJECTED" => OperationStatus.Rejected,
            _ => throw new ArgumentException($"Invalid result: {request.Result}")
        };

        operation.ChangeStatus(newStatus, $"Receipt received: {request.Result}");
        await _repository.SaveChangesAsync(ct);
    }

    public async Task<OperationResponse?> GetOperationAsync(string operationId, CancellationToken ct = default)
    {
        var operation = await _repository.GetByIdAsync(operationId, ct);
        return operation == null ? null : MapToResponse(operation);
    }

    public async Task<IReadOnlyList<OperationEventResponse>> GetOperationEventsAsync(string operationId, CancellationToken ct = default)
    {
        var operation = await _repository.GetByIdAsync(operationId, ct)
            ?? throw new NotFoundException($"Operation {operationId} not found");

        // Sort chronologically and generate monotonic EventId dynamically to match API contract
        var sortedEvents = operation.Events.OrderBy(e => e.Timestamp).ToList();

        return sortedEvents.Select((e, index) => new OperationEventResponse
        {
            EventId = index + 1,
            Type = e.NewStatus.ToString().ToUpper(),
            FromStatus = e.OldStatus == OperationStatus.None ? null : e.OldStatus.ToString().ToUpper(),
            ToStatus = e.NewStatus.ToString().ToUpper(),
            Message = e.Reason,
            OccurredAt = e.Timestamp
        }).ToList();
    }

    private static OperationResponse MapToResponse(Operation operation)
    {
        return new OperationResponse
        {
            OperationId = operation.OperationId,
            Amount = operation.Amount.ToString("F2", CultureInfo.InvariantCulture),
            Currency = operation.Currency,
            Description = operation.Description,
            Status = operation.Status.ToString().ToUpper(),
            ProviderPaymentId = operation.ProviderPaymentId
        };
    }
}