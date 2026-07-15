namespace Application.DTOs;

public class OperationEventResponse
{
    public string Id { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}