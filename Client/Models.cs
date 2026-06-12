using JsonSubTypes;
using Newtonsoft.Json;

namespace SmsProxyHub.Client;

/// <summary>
/// Payload delivered to the consumer's webhook when an SMS reply is received.
/// </summary>
public sealed class WebhookCallbackPayload
{
    [JsonProperty("fromPhone")]
    public string FromPhone { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("originalPayload")]
    public string? OriginalPayload { get; set; }

    [JsonProperty("connectionId")]
    public Guid ConnectionId { get; set; }

    [JsonProperty("receivedAt")]
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class SendSmsRequest
{
    [JsonProperty("connectionId")]
    public Guid ConnectionId { get; set; }

    [JsonProperty("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional JSON payload that will be echoed back in the webhook callback when a reply is received.
    /// </summary>
    [JsonProperty("payload")]
    public string? Payload { get; set; }
}

public sealed class SendSmsResponse
{
    [JsonProperty("messageId")]
    public string? MessageId { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
}
