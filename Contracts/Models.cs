using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmsProxyHub.Contracts
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum WebhookEventType
    {
        SmsSent,
        SmsFailed,
        SmsDelivered,
        SmsReply
    }

    public sealed class WebhookCallbackPayload
    {
        [JsonProperty("event")]
        public WebhookEventType Event { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("originalPayload")]
        public string OriginalPayload { get; set; }

        [JsonProperty("connectionId")]
        public System.Guid ConnectionId { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("timestamp")]
        public System.DateTimeOffset Timestamp { get; set; }
    }

    public sealed class SendSmsRequest
    {
        [JsonProperty("connectionId")]
        public System.Guid? ConnectionId { get; set; }

        [JsonProperty("phoneNumbers")]
        public string[] PhoneNumbers { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

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
        public System.Guid? UsedConnectionId { get; set; }
    }

    public sealed class BulkSendSmsResponse
    {
        [JsonProperty("results")]
        public SendSmsResponse[] Results { get; set; }
    }
}
