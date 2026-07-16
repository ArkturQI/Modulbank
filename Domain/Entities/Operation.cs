using Domain.Enums;

namespace Domain.Entities;

public class Operation
{
    private Operation() { }

    public Guid Id { get; private set; }
    public string OperationId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty; 

    public string? ProviderPaymentId { get; private set; }
    public OperationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = new byte[0];

    private readonly List<OperationEvent> _events = new();
    public IReadOnlyCollection<OperationEvent> Events => _events.AsReadOnly();

    public static Operation Create(string operationId, decimal amount, string currency, string description)
    {
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            Amount = amount,
            Currency = currency,
            Description = description, 
            Status = OperationStatus.Created,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        operation._events.Add(new OperationEvent(operation.Id, OperationStatus.None, OperationStatus.Created, "Operation created"));
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

    public void RecordIgnoredReceipt(string result)
    {
        _events.Add(new OperationEvent(Id, Status, Status, $"Ignored late receipt: {result}"));
        UpdatedAt = DateTimeOffset.UtcNow;
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