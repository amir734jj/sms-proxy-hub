namespace Shared.Contracts;

public class WebhookDeliveryDto
{
    public Guid Id { get; init; }
    public Guid ConnectionId { get; init; }
    public string Event { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public int? HttpStatus { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
