using Api.Interfaces;

namespace Api.Data.Entities;

public sealed class WebhookDelivery : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WebhookSubscriptionId { get; set; }
    public Guid ConnectionId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public int? HttpStatus { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
