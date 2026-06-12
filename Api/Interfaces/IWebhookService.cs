using Api.Data.Entities;
using Shared.Contracts;

namespace Api.Interfaces;

public interface IWebhookService
{
    Task<List<WebhookSubscriptionDto>> GetAllForUserAsync(Guid userId);
    Task<WebhookSubscriptionDto> CreateAsync(Guid userId, CreateWebhookRequest request);
    Task<bool> DeleteAsync(Guid userId, Guid id);
    Task<List<WebhookSubscription>> GetActiveForConnectionAsync(Guid connectionId);
    Task DeliverWebhookAsync(WebhookSubscription subscription, string fromPhone, string message, string? originalPayload, Guid connectionId);
}
