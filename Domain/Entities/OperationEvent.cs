using Domain.Enums;

namespace Domain.Entities;

public class OperationEvent
{
    public Guid Id { get; private set; }
    public Guid OperationId { get; private set; }
    public OperationStatus OldStatus { get; private set; }
    public OperationStatus NewStatus { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset Timestamp { get; private set; }
    private OperationEvent() { }

    public OperationEvent(Guid operationId, OperationStatus oldStatus, OperationStatus newStatus, string reason)
    {
        Id = Guid.NewGuid();
        OperationId = operationId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Reason = reason;
        Timestamp = DateTimeOffset.UtcNow;
    }
}