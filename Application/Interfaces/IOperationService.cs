using Application.DTOs;
using Domain.Entities;

namespace Application.Interfaces;

public interface IOperationService
{
    Task<OperationResponse> CreateOperationAsync(CreateOperationRequest request, CancellationToken ct = default);

    // Returns the operation and a flag indicating if this was the first submission attempt
    Task<(Operation Operation, bool IsFirstSubmission)> SubmitOperationAsync(string operationId, CancellationToken ct = default);

    Task ProcessReceiptAsync(ReceiptRequest request, CancellationToken ct = default);
    Task<OperationResponse?> GetOperationAsync(string operationId, CancellationToken ct = default);
    Task<IReadOnlyList<OperationEventResponse>> GetOperationEventsAsync(string operationId, CancellationToken ct = default);
}