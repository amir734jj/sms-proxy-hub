using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shared.Contracts;

[JsonConverter(typeof(StringEnumConverter))]
public enum WebhookEventType
{
    SmsSent,
    SmsFailed,
    SmsDelivered,
    SmsReply
}
