using System.Text;
using Api.Data.Entities;
using Api.Interfaces;
using EfCoreRepository.Interfaces;
using EfCoreRepository.Extensions;
using EfCoreRepository.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Contracts;

namespace Api.Services;

public sealed class WebhookService(
    IEfRepository repository,
    IConnectionService connectionService,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookService> logger) : IWebhookService
{
    private IBasicCrud<WebhookSubscription> Dal => repository.For<WebhookSubscription>();

    public async Task<List<WebhookSubscriptionDto>> GetAllForUserAsync(Guid userId)
    {
        var connections = await connectionService.GetAllForUserAsync(userId);
        var connectionIds = connections.Select(c => c.Id).ToList();

        if (connectionIds.Count == 0) return [];

        return (await Dal.GetAll(
            filterExprs: [w => connectionIds.Contains(w.ConnectionId)],
            orderBy: Ordering<WebhookSubscription>.Desc(w => w.CreatedAt),
            project: w => new WebhookSubscriptionDto(w.Id, w.ConnectionId, w.Url, w.IsActive, w.CreatedAt)
        )).ToList();
    }

    public async Task<WebhookSubscriptionDto> CreateAsync(Guid userId, CreateWebhookRequest request)
    {
        if (!await connectionService.UserOwnsConnectionAsync(userId, request.ConnectionId))
        {
            throw new InvalidOperationException("Connection not found.");
        }

        var entity = await Dal.Save(new WebhookSubscription
        {
            ConnectionId = request.ConnectionId,
            Url = request.Url.Trim()
        });

        return new WebhookSubscriptionDto(entity.Id, entity.ConnectionId, entity.Url, entity.IsActive, entity.CreatedAt);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id)
    {
        var items = (await Dal.GetAll(
            filterExprs: [w => w.Id == id],
            maxResults: 1)).ToList();

        if (items.Count == 0) return false;

        var webhook = items.First();
        if (!await connectionService.UserOwnsConnectionAsync(userId, webhook.ConnectionId))
            return false;

        await Dal.Delete(id);
        return true;
    }

    public async Task<List<WebhookSubscription>> GetActiveForConnectionAsync(Guid connectionId)
    {
        return (await Dal.GetAll(
            filterExprs: [w => w.ConnectionId == connectionId && w.IsActive]
        )).ToList();
    }

    public async Task DeliverToAllAsync(Guid connectionId, WebhookEventType eventType,
        string phone, string? message, string? originalPayload, string? reason = null,
        string? subject = null, JToken? attachments = null)
    {
        var subscriptions = await GetActiveForConnectionAsync(connectionId);
        foreach (var sub in subscriptions)
        {
            await DeliverWebhookAsync(sub, eventType, phone, message, originalPayload, connectionId, reason, subject, attachments);
        }
    }

    public async Task DeliverWebhookAsync(
        WebhookSubscription subscription, WebhookEventType eventType,
        string phone, string? message, string? originalPayload, Guid connectionId, string? reason = null,
        string? subject = null, JToken? attachments = null)
    {
        var phoneE164 = PhoneUtility.NormalizePhoneNumber(phone) ?? phone;
        var phoneDigits = ToDigits(phoneE164);
        var phoneNational = ToUsNationalDigits(phoneDigits);
        var callbackPhone = phoneNational ?? phoneE164;

        var callbackPayload = new
        {
            @event = eventType.ToString(),
            phone = callbackPhone,
            phoneE164,
            phoneDigits,
            phoneNational,
            message,
            subject,
            attachments,
            originalPayload,
            connectionId,
            reason,
            timestamp = DateTimeOffset.UtcNow
        };

        var json = JsonConvert.SerializeObject(callbackPayload);

        // The full payload (including message text/subject/attachments) is delivered live to the
        // destination webhook, but message content is never persisted at rest. Store a redacted copy.
        var storedJson = JsonConvert.SerializeObject(new
        {
            @event = eventType.ToString(),
            phone = callbackPhone,
            phoneE164,
            phoneDigits,
            phoneNational,
            message = (string?)null,
            subject = (string?)null,
            attachments = (JToken?)null,
            originalPayload,
            connectionId,
            reason,
            timestamp = callbackPayload.timestamp
        });

        var delivery = new WebhookDelivery
        {
            WebhookSubscriptionId = subscription.Id,
            ConnectionId = connectionId,
            Event = eventType.ToString(),
            Url = subscription.Url,
            RequestBody = storedJson
        };

        try
        {
            var httpClient = httpClientFactory.CreateClient();
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url) { Content = content };

            var response = await httpClient.SendAsync(request);
            delivery.HttpStatus = (int)response.StatusCode;
            delivery.Success = response.IsSuccessStatusCode;

            logger.LogInformation("Webhook delivered to {Url}, status {Status}",
                subscription.Url, response.StatusCode);
        }
        catch (Exception ex)
        {
            delivery.Success = false;
            delivery.Error = ex.Message;
            logger.LogWarning(ex, "Failed to deliver webhook to {Url}", subscription.Url);
        }

        try { await repository.For<WebhookDelivery>().Save(delivery); }
        catch (Exception ex) { logger.LogError(ex, "Failed to save webhook delivery log"); }
    }

    public async Task<List<WebhookDeliveryDto>> GetDeliveriesForUserAsync(Guid userId, int limit = 50)
    {
        var connections = await connectionService.GetAllForUserAsync(userId);
        var connectionIds = connections.Select(c => c.Id).ToList();
        if (connectionIds.Count == 0) return [];

        return (await repository.For<WebhookDelivery>().GetAll(
            filterExprs: [d => connectionIds.Contains(d.ConnectionId)],
            orderBy: Ordering<WebhookDelivery>.Desc(d => d.CreatedAt),
            maxResults: limit,
            project: d => new WebhookDeliveryDto
            {
                Id = d.Id,
                ConnectionId = d.ConnectionId,
                Event = d.Event,
                Url = d.Url,
                Payload = d.RequestBody,
                HttpStatus = d.HttpStatus,
                Success = d.Success,
                Error = d.Error,
                CreatedAt = d.CreatedAt
            }
        )).ToList();
    }

    public async Task<bool> ReplayAsync(Guid userId, Guid deliveryId)
    {
        var connections = await connectionService.GetAllForUserAsync(userId);
        var connectionIds = connections.Select(c => c.Id).ToHashSet();

        var deliveries = (await repository.For<WebhookDelivery>().GetAll(
            filterExprs: [d => d.Id == deliveryId], maxResults: 1)).ToList();

        if (deliveries.Count == 0) return false;
        var original = deliveries.First();
        if (!connectionIds.Contains(original.ConnectionId)) return false;

        var replay = new WebhookDelivery
        {
            WebhookSubscriptionId = original.WebhookSubscriptionId,
            ConnectionId = original.ConnectionId,
            Event = original.Event,
            Url = original.Url,
            RequestBody = original.RequestBody
        };

        try
        {
            var httpClient = httpClientFactory.CreateClient();
            using var content = new StringContent(original.RequestBody, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, original.Url) { Content = content };

            var response = await httpClient.SendAsync(request);
            replay.HttpStatus = (int)response.StatusCode;
            replay.Success = response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            replay.Success = false;
            replay.Error = ex.Message;
        }

        try { await repository.For<WebhookDelivery>().Save(replay); }
        catch (Exception ex) { logger.LogError(ex, "Failed to save replay delivery log"); }

        return true;
    }

    private static string ToDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string? ToUsNationalDigits(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits)) return null;
        if (digits.Length == 11 && digits.StartsWith('1')) return digits[1..];
        if (digits.Length == 10) return digits;
        return null;
    }
}
