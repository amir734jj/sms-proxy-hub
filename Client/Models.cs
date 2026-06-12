using System;
using Newtonsoft.Json;

namespace SmsProxyHub.Client
{
    /// <summary>
    /// Payload delivered to the consumer's webhook on SMS events.
    /// </summary>
    public sealed class WebhookCallbackPayload
    {
        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("originalPayload")]
        public string OriginalPayload { get; set; }

        [JsonProperty("connectionId")]
        public Guid ConnectionId { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("timestamp")]
        public DateTimeOffset Timestamp { get; set; }
    }

    public sealed class SendSmsRequest
    {
        /// <summary>
        /// Optional. If null, tries all active connections in priority order (failover).
        /// </summary>
        [JsonProperty("connectionId")]
        public Guid? ConnectionId { get; set; }

        [JsonProperty("phoneNumber")]
        public string PhoneNumber { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>
        /// Optional payload that will be echoed back in the webhook callback when a reply is received.
        /// Can be any object - it gets serialized to JSON automatically.
        /// </summary>
        [JsonProperty("payload")]
        public object Payload { get; set; }
    }

    public sealed class SendSmsResponse
    {
        [JsonProperty("messageId")]
        public string MessageId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("usedConnectionId")]
        public Guid? UsedConnectionId { get; set; }
    }
}
