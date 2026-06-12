using Api.Data.Entities;
using Shared.Contracts;

namespace Api.Interfaces;

public interface IWebhookService
{
    Task<List<WebhookSubscriptionDto>> GetAllForUserAsync(Guid userId);
    Task<WebhookSubscriptionDto> CreateAsync(Guid userId, CreateWebhookRequest request);
    Task<bool> DeleteAsync(Guid userId, Guid id);
    Task<List<WebhookSubscription>> GetActiveForConnectionAsync(Guid connectionId);
    Task DeliverWebhookAsync(WebhookSubscription subscription, WebhookEventType eventType,
        string phone, string? message, string? originalPayload, Guid connectionId, string? reason = null);
    Task DeliverToAllAsync(Guid connectionId, WebhookEventType eventType,
        string phone, string? message, string? originalPayload, string? reason = null);
    Task<List<WebhookDeliveryDto>> GetDeliveriesForUserAsync(Guid userId, int limit = 50);
}
