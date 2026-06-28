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
    IDeviceStatusService deviceStatusService,
    ILogger<ProviderWebhookController> logger) : ControllerBase
{
    // POST /api/provider-webhook/{connectionId}
    // Each connection has its own webhook URL to register with the SMS provider.
    [HttpPost("{connectionId:guid}")]
    public async Task<IActionResult> Receive(Guid connectionId)
    {
        // Buffer the body up-front so we can log the raw payload and still let the provider parse it.
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
        }
        Request.Body.Position = 0;

        logger.LogInformation(
            "Inbound provider webhook for connection {ConnectionId} from {RemoteIp} (ContentType={ContentType}, Length={Length}). Body: {Body}",
            connectionId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.ContentType,
            rawBody.Length,
            rawBody);

        try
        {
            return await ProcessAsync(connectionId, rawBody);
        }
        catch (Exception ex)
        {
            // Surface the real cause (otherwise the provider just sees an opaque 500) and let it retry.
            logger.LogError(ex,
                "Error processing provider webhook for connection {ConnectionId}. Body: {Body}",
                connectionId, rawBody);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<IActionResult> ProcessAsync(Guid connectionId, string rawBody)
    {
        var connection = await connectionService.GetByIdAsync(connectionId);
        if (connection is null)
        {
            logger.LogWarning("Webhook received for unknown connection {ConnectionId}", connectionId);
            return NotFound();
        }

        // Device status tracking is SMS Gate-specific (system:ping health payload).
        if (connection.ProviderType == SmsProviderType.SmsGate)
        {
            deviceStatusService.Record(connectionId, rawBody);
        }

        var config = JsonConvert.DeserializeObject<SmsConnectionConfig>(connection.ConfigJson);
        if (config is null)
        {
            logger.LogError("Failed to deserialize config for connection {ConnectionId}", connectionId);
            return Ok();
        }

        // Let the provider parse the webhook (body already buffered by the caller)
        Request.Body.Position = 0;

        var provider = providerFactory.GetProvider(connection.ProviderType);
        var incoming = await provider.ParseWebhookAsync(Request, config);

        if (incoming is null)
        {
            // Not a reply - it may be a delivery receipt (sms:delivered) we forward as SmsDelivered.
            Request.Body.Position = 0;
            var receipt = await provider.ParseDeliveryWebhookAsync(Request, config);
            if (receipt is not null)
            {
                var recipient = PhoneUtility.NormalizePhoneNumber(receipt.RecipientPhone) ?? receipt.RecipientPhone;
                var delivered = await messageService.FindSentByProviderIdOrPhoneAsync(connectionId, receipt.ProviderMessageId, recipient);

                logger.LogInformation(
                    "Delivery receipt for connection {ConnectionId} (message {MessageId}) to {Phone}; forwarding SmsDelivered",
                    connectionId, receipt.ProviderMessageId ?? "none", recipient);

                await webhookService.DeliverToAllAsync(connectionId, WebhookEventType.SmsDelivered,
                    recipient, delivered?.Message, delivered?.Payload);
                return Ok();
            }

            logger.LogInformation(
                "Provider webhook for {ConnectionId}: nothing actionable parsed (non-inbound event, wrong device, or unparseable payload)",
                connectionId);
            return Ok();
        }

        var normalizedPhone = PhoneUtility.NormalizePhoneNumber(incoming.FromPhone) ?? incoming.FromPhone;
        logger.LogInformation("SMS received from {Phone} on connection {ConnectionId}: {Message}",
            normalizedPhone, connectionId, incoming.Message);

        // Find the original outbound message to get the user's payload
        var originalMessage = await messageService.FindLatestSentToPhoneAsync(connectionId, normalizedPhone);
        string? originalPayload = originalMessage?.Payload;

        logger.LogInformation(
            "Webhook for connection {ConnectionId}: matched original message {Matched}; forwarding reply from {Phone} to subscriptions",
            connectionId,
            originalMessage is not null ? originalMessage.Id.ToString() : "none",
            normalizedPhone);

        // Forward to all active webhook subscriptions for this connection
        await webhookService.DeliverToAllAsync(connectionId, WebhookEventType.SmsReply,
            normalizedPhone, incoming.Message, originalPayload,
            subject: incoming.Subject, attachments: incoming.Attachments);

        // Mark reply received after successful delivery
        if (originalMessage is not null)
        {
            await messageService.MarkReplyReceivedAsync(originalMessage.Id);
        }

        return Ok();
    }
}
