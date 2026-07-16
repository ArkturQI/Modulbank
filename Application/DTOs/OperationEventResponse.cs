namespace Application.DTOs;

public class OperationEventResponse
{
    public int EventId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}