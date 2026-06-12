using JsonSubTypes;
using Newtonsoft.Json;

namespace Shared.Contracts;

[JsonConverter(typeof(JsonSubtypes), "Type")]
[JsonSubtypes.KnownSubType(typeof(SmsGateConnectionConfig), "smsgate")]
[JsonSubtypes.KnownSubType(typeof(TwilioConnectionConfig), "twilio")]
public abstract class SmsConnectionConfig
{
    public abstract string Type { get; }
}

public sealed class SmsGateConnectionConfig : SmsConnectionConfig
{
    public override string Type => "smsgate";
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class TwilioConnectionConfig : SmsConnectionConfig
{
    public override string Type => "twilio";
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
}
