using Newtonsoft.Json.Linq;
using Shared.Contracts;

namespace Api.Interfaces;

/// <summary>
/// Interface that all SMS providers must implement.
/// Add a new provider by implementing this interface and registering it in DI.
/// </summary>
public interface ISmsProvider
{
    /// <summary>
    /// Provider type identifier. Must match <see cref="SmsConnectionConfig.Type"/>.
    /// </summary>
    SmsProviderType ProviderType { get; }

    /// <summary>
    /// Send an SMS through this provider.
    /// </summary>
    /// <returns>Provider message ID on success, null on failure.</returns>
    Task<string?> SendAsync(string to, string message, SmsConnectionConfig config);

    /// <summary>
    /// Parse an incoming webhook request from this provider.
    /// </summary>
    /// <returns>Parsed incoming SMS or null if the request is not a valid SMS event.</returns>
    Task<IncomingSms?> ParseWebhookAsync(HttpRequest request, SmsConnectionConfig config);

    /// <summary>
    /// Parse a delivery-receipt webhook (e.g. SMS Gate sms:delivered, Twilio MessageStatus=delivered).
    /// </summary>
    /// <returns>A delivery receipt, or null if the request is not a delivery event.</returns>
    Task<DeliveryReceipt?> ParseDeliveryWebhookAsync(HttpRequest request, SmsConnectionConfig config);
}

// A parsed inbound reply. MMS replies additionally carry Subject and Attachments
// (media), which SMS replies don't; Attachments is the provider's raw array so no
// information is lost on the way to the destination.
public sealed record IncomingSms(
    string FromPhone,
    string Message,
    string? ProviderMessageId,
    string? Subject = null,
    JToken? Attachments = null);

// A carrier delivery receipt for a message we previously sent (recipient confirmed delivery).
public sealed record DeliveryReceipt(string? ProviderMessageId, string RecipientPhone);
