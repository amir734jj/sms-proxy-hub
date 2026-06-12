using Api.Interfaces;
using Shared.Contracts;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Api.Providers;

public sealed class TwilioProvider(ILogger<TwilioProvider> logger) : ISmsProvider
{
    public SmsProviderType ProviderType => SmsProviderType.Twilio;

    public async Task<string?> SendAsync(string to, string message, SmsConnectionConfig config)
    {
        if (config is not TwilioConnectionConfig twilio)
        {
            logger.LogError("Invalid config type for Twilio provider");
            return null;
        }

        try
        {
            TwilioClient.Init(twilio.AccountSid, twilio.AuthToken);

            var result = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(twilio.FromNumber),
                to: new PhoneNumber(to));

            logger.LogInformation("Twilio SMS sent, SID={Sid}", result.Sid);
            return result.Sid;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Twilio send failed");
            return null;
        }
    }

    public Task<IncomingSms?> ParseWebhookAsync(HttpRequest request, SmsConnectionConfig config)
    {
        // Twilio sends webhooks as form-encoded POST
        if (!request.HasFormContentType)
            return Task.FromResult<IncomingSms?>(null);

        var form = request.Form;
        var from = form["From"].ToString();
        var body = form["Body"].ToString().Trim();
        var messageSid = form["MessageSid"].ToString();

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
            return Task.FromResult<IncomingSms?>(null);

        return Task.FromResult<IncomingSms?>(new IncomingSms(from, body, messageSid));
    }
}
