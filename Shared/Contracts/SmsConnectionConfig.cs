using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shared.Contracts;

public enum SmsProviderType
{
    SmsGate,
    Twilio
}

[JsonConverter(typeof(JsonSubtypes), "Type")]
[JsonSubtypes.KnownSubType(typeof(SmsGateConnectionConfig), SmsProviderType.SmsGate)]
[JsonSubtypes.KnownSubType(typeof(TwilioConnectionConfig), SmsProviderType.Twilio)]
public abstract class SmsConnectionConfig
{
    [JsonConverter(typeof(StringEnumConverter))]
    public abstract SmsProviderType Type { get; }
}

public sealed class SmsGateConnectionConfig : SmsConnectionConfig
{
    public override SmsProviderType Type => SmsProviderType.SmsGate;
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class TwilioConnectionConfig : SmsConnectionConfig
{
    public override SmsProviderType Type => SmsProviderType.Twilio;
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
}
