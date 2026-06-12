using Api.Interfaces;

namespace Api.Data.Entities;

public sealed class WebhookSubscription : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
