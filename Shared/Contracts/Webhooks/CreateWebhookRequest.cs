namespace Shared.Contracts;

public record CreateWebhookRequest(Guid ConnectionId, string Url, string? Secret);
