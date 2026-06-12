using Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Shared;
using Shared.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("api/provider-webhook")]
public sealed class ProviderWebhookController(
    ISmsProviderFactory providerFactory,
    IConnectionService connectionService,
    IMessageService messageService,
    IWebhookService webhookService,
    ILogger<ProviderWebhookController> logger) : ControllerBase
{
    /// <summary>
    /// Receives webhook callbacks from SMS providers (SmsGate, Twilio, etc.).
    /// Route: POST /api/provider-webhook/{connectionId}
    /// Each connection gets a unique webhook URL to register with the provider.
    /// </summary>
    [HttpPost("{connectionId:guid}")]
    public async Task<IActionResult> Receive(Guid connectionId)
    {
        var connection = await connectionService.GetByIdAsync(connectionId);
        if (connection is null)
        {
            logger.LogWarning("Webhook received for unknown connection {ConnectionId}", connectionId);
            return NotFound();
        }

        var config = JsonConvert.DeserializeObject<SmsConnectionConfig>(connection.ConfigJson);
        if (config is null)
        {
            logger.LogError("Failed to deserialize config for connection {ConnectionId}", connectionId);
            return Ok();
        }

        // Let the provider parse the webhook
        Request.EnableBuffering();
        Request.Body.Position = 0;

        var provider = providerFactory.GetProvider(connection.ProviderType);
        var incoming = await provider.ParseWebhookAsync(Request, config);

        if (incoming is null)
        {
            logger.LogInformation("Provider webhook for {ConnectionId}: no SMS parsed (non-SMS event)", connectionId);
            return Ok();
        }

        var normalizedPhone = PhoneUtility.NormalizePhoneNumber(incoming.FromPhone) ?? incoming.FromPhone;
        logger.LogInformation("SMS received from {Phone} on connection {ConnectionId}: {Message}",
            normalizedPhone, connectionId, incoming.Message);

        // Find the original outbound message to get the user's payload
        var originalMessage = await messageService.FindLatestSentToPhoneAsync(connectionId, normalizedPhone);
        string? originalPayload = originalMessage?.Payload;

        if (originalMessage is not null)
        {
            await messageService.MarkReplyReceivedAsync(originalMessage.Id);
        }

        // Forward to all active webhook subscriptions for this connection
        var subscriptions = await webhookService.GetActiveForConnectionAsync(connectionId);
        foreach (var sub in subscriptions)
        {
            await webhookService.DeliverWebhookAsync(
                sub, normalizedPhone, incoming.Message, originalPayload, connectionId);
        }

        return Ok();
    }
}
