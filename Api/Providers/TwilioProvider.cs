using Api.Interfaces;
using Shared.Contracts;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.IncomingPhoneNumber;
using Twilio.Types;

namespace Api.Providers;

public sealed class TwilioProvider(IConfiguration configuration, ILogger<TwilioProvider> logger) : ISmsProvider
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
            var client = new Twilio.Clients.TwilioRestClient(twilio.AccountSid, twilio.AuthToken);

            var result = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(twilio.FromNumber),
                to: new PhoneNumber(to),
                client: client);

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

    public async Task RegisterWebhookAsync(TwilioConnectionConfig config, Guid connectionId)
    {
        var publicUrl = configuration["App:PublicUrl"];
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            logger.LogWarning("App:PublicUrl not configured, skipping Twilio webhook registration");
            return;
        }

        try
        {
            var client = new TwilioRestClient(config.AccountSid, config.AuthToken);
            var webhookUrl = new Uri($"{publicUrl.TrimEnd('/')}/api/provider-webhook/{connectionId}");

            // Find the phone number resource
            var numbers = await IncomingPhoneNumberResource.ReadAsync(
                phoneNumber: new PhoneNumber(config.FromNumber),
                client: client);

            var number = numbers.FirstOrDefault();
            if (number is null)
            {
                logger.LogWarning("Twilio phone number {Number} not found in account", config.FromNumber);
                return;
            }

            await IncomingPhoneNumberResource.UpdateAsync(
                number.Sid,
                smsUrl: webhookUrl,
                smsMethod: Twilio.Http.HttpMethod.Post,
                client: client);

            logger.LogInformation("Twilio webhook registered for {Number} -> {Url}", config.FromNumber, webhookUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register Twilio webhook for connection {ConnectionId}", connectionId);
        }
    }
}
