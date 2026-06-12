namespace Shared.Contracts;

public record WebhookSubscriptionDto(Guid Id, Guid ConnectionId, string Url, bool IsActive, DateTimeOffset CreatedAt);
