using Api.Interfaces;

namespace Api.Data.Entities;

public sealed class InboundWebhookReceipt : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionId { get; set; }
    public string ProviderMessageId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
