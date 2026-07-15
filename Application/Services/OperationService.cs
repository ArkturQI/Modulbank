using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

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
        // Generate OperationId if not provided (idempotent creation)
        var operationId = request.OperationId ?? Guid.NewGuid().ToString();

        // Check if operation already exists (idempotency)
        var existing = await _repository.GetByIdAsync(operationId, ct);
        if (existing != null)
        {
            return MapToResponse(existing);
        }

        // Create new operation
        var operation = Operation.Create(operationId);
        await _repository.AddAsync(operation, ct);

        return MapToResponse(operation);
    }

    public async Task SubmitOperationAsync(string operationId, CancellationToken ct = default)
    {
        var operation = await _repository.GetByIdAsync(operationId, ct)
            ?? throw new InvalidOperationException($"Operation {operationId} not found");

        // Idempotency: if already in final state, do nothing
        if (operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Rejected)
        {
            return;
        }

        // Idempotency: if already processing, do nothing
        if (operation.Status == OperationStatus.Processing)
        {
            return;
        }

        // Change status to Processing
        operation.ChangeStatus(OperationStatus.Processing, "Submit called");
        await _repository.SaveChangesAsync(ct);
    }

    public async Task ProcessReceiptAsync(ReceiptRequest request, CancellationToken ct = default)
    {
        var operation = await _repository.GetByIdAsync(request.OperationId, ct)
            ?? throw new InvalidOperationException($"Operation {request.OperationId} not found");

        // Protect against race conditions: ProviderPaymentId must not be overwritten
        if (!operation.TrySetProviderPaymentId(request.ProviderPaymentId))
        {
            throw new InvalidOperationException(
                $"ProviderPaymentId mismatch. Existing: {operation.ProviderPaymentId}, New: {request.ProviderPaymentId}");
        }

        // Ignore late receipts if operation already in final state
        if (operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Rejected)
        {
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
            ?? throw new InvalidOperationException($"Operation {operationId} not found");

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