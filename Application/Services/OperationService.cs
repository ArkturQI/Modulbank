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

        var operation = Operation.Create(operationId);
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
            // Race condition fallback: another request updated the row simultaneously.
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
            // Log the ignored late receipt in the event history without changing the final status
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

        return operation.Events
            .Select(e => new OperationEventResponse
            {
                Id = e.Id.ToString(),
                OldStatus = e.OldStatus.ToString(),
                NewStatus = e.NewStatus.ToString(),
                Reason = e.Reason,
                Timestamp = e.Timestamp
            })
            .ToList();
    }

    private static OperationResponse MapToResponse(Operation operation)
    {
        return new OperationResponse
        {
            Id = operation.Id.ToString(),
            Status = operation.Status.ToString(),
            ProviderPaymentId = operation.ProviderPaymentId,
            CreatedAt = operation.CreatedAt
        };
    }
}