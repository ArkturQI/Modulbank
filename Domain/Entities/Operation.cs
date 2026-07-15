using Domain.Enums;

namespace Domain.Entities;

public class Operation
{
    public Guid Id { get; private set; }
    public string OperationId { get; private set; } = string.Empty;
    public string? ProviderPaymentId { get; private set; }
    public OperationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<OperationEvent> _events = new();
    public IReadOnlyCollection<OperationEvent> Events => _events.AsReadOnly();

    
    private Operation() { }

    public static Operation Create(string operationId)
    {
        var operation = new Operation
        {
            Id = Guid.NewGuid(), 
            OperationId = operationId,
            Status = OperationStatus.Created,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        operation._events.Add(new OperationEvent(
            operation.Id,
            OperationStatus.None,
            OperationStatus.Created,
            "Operation created"));

        return operation;
    }

    public void ChangeStatus(OperationStatus newStatus, string reason)
    {
        if (Status == newStatus) return;

        var oldStatus = Status;
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;

        _events.Add(new OperationEvent(Id, oldStatus, newStatus, reason));
    }

    public bool TrySetProviderPaymentId(string providerPaymentId)
    {
        if (string.IsNullOrEmpty(ProviderPaymentId))
        {
            ProviderPaymentId = providerPaymentId;
            return true;
        }

        return ProviderPaymentId == providerPaymentId;
    }
}