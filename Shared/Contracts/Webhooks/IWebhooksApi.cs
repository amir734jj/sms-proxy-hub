using Refit;

namespace Shared.Contracts.Interfaces;

public interface IWebhooksApi
{
    [Get("/api/webhooks")]
    [Headers("Authorization: Bearer")]
    Task<List<WebhookSubscriptionDto>> GetAllAsync();

    [Post("/api/webhooks")]
    [Headers("Authorization: Bearer")]
    Task<WebhookSubscriptionDto> CreateAsync([Body] CreateWebhookRequest request);

    [Delete("/api/webhooks/{id}")]
    [Headers("Authorization: Bearer")]
    Task DeleteAsync(Guid id);

    [Get("/api/webhooks/deliveries")]
    [Headers("Authorization: Bearer")]
    Task<List<WebhookDeliveryDto>> GetDeliveriesAsync([Query] int limit = 50);
}
