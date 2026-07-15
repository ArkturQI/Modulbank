using Application.DTOs;

namespace Application.Interfaces
{
    public interface IOperationService
    {
        Task<OperationResponse> CreateOperationAsync(CreateOperationRequest request, CancellationToken ct = default);
        Task SubmitOperationAsync(string operationId, CancellationToken ct = default);
        Task ProcessReceiptAsync(ReceiptRequest request, CancellationToken ct = default);
        Task<OperationResponse?> GetOperationAsync(string operationId, CancellationToken ct = default);
        Task<IReadOnlyList<OperationEventResponse>> GetOperationEventsAsync(string operationId, CancellationToken ct = default);
    }
}
