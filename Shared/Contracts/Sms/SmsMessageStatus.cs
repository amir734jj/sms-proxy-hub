using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shared.Contracts;

[JsonConverter(typeof(StringEnumConverter))]
public enum SmsMessageStatus
{
    Sent = 0,
    Failed = 1,
    Delivered = 2,
    ReplyReceived = 3,
}
