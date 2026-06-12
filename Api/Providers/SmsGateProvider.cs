using System.Net.Http.Headers;
using System.Text;
using Api.Generated.SmsGate;
using Api.Interfaces;
using Newtonsoft.Json.Linq;
using Shared.Contracts;
using Message = Api.Generated.SmsGate.Message;

namespace Api.Providers;

public sealed class SmsGateProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SmsGateProvider> logger) : ISmsProvider
{
    public SmsProviderType ProviderType => SmsProviderType.SmsGate;

    public async Task<string?> SendAsync(string to, string message, SmsConnectionConfig config)
    {
        if (config is not SmsGateConnectionConfig smsGate)
        {
            logger.LogError("Invalid config type for SmsGate provider");
            return null;
        }

        try
        {
            var client = await CreateAuthenticatedClientAsync(smsGate);
            if (client is null) return null;

            var request = new Message
            {
                PhoneNumbers = [to],
                TextMessage = new TextMessage { Text = message },
                DeviceId = smsGate.DeviceId
            };

            var response = await client.MessagesPOSTAsync(request);
            return response.Id;
        }
        catch (SmsGateApiException ex)
        {
            logger.LogWarning("SmsGate returned {StatusCode}: {Response}", ex.StatusCode, ex.Response);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate send failed");
            return null;
        }
    }

    public async Task<IncomingSms?> ParseWebhookAsync(HttpRequest request, SmsConnectionConfig config)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        var root = JObject.Parse(body);

        if (root["event"]?.ToString() != "sms:received")
            return null;

        var payload = root["payload"];
        if (payload is null)
            return null;

        // When deviceId is configured, ignore webhooks from other devices
        if (config is SmsGateConnectionConfig smsGate
            && !string.IsNullOrWhiteSpace(smsGate.DeviceId))
        {
            var webhookDeviceId = root["deviceId"]?.ToString();
            if (webhookDeviceId != smsGate.DeviceId)
            {
                logger.LogInformation("Ignoring webhook from device {WebhookDevice}, expected {ConfiguredDevice}",
                    webhookDeviceId, smsGate.DeviceId);
                return null;
            }
        }

        return new IncomingSms(
            payload["sender"]?.ToString() ?? "",
            payload["message"]?.ToString().Trim() ?? "",
            payload["messageId"]?.ToString());
    }

    public async Task<List<Device>> GetDevicesAsync(SmsGateConnectionConfig config)
    {
        try
        {
            var client = await CreateAuthenticatedClientAsync(config);
            if (client is null) return [];

            var devices = await client.DevicesAllAsync();
            return devices.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate list devices failed");
            return [];
        }
    }

    public async Task RegisterWebhookAsync(SmsGateConnectionConfig config, Guid connectionId)
    {
        try
        {
            var client = await CreateAuthenticatedClientAsync(config, includeWebhookScope: true);
            if (client is null) return;

            var webhookUrl = $"{configuration["App:PublicUrl"]!.TrimEnd('/')}/api/provider-webhook/{connectionId}";
            var webhookId = $"sms-proxy-hub-{connectionId}";

            await client.WebhooksPOSTAsync(new Webhook
            {
                Id = webhookId,
                Event = WebhookEvent.SmsReceived,
                Url = webhookUrl,
                DeviceId = config.DeviceId
            });

            logger.LogInformation("SMS Gate webhook registered: {WebhookId} -> {Url}", webhookId, webhookUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register SMS Gate webhook for connection {ConnectionId}", connectionId);
        }
    }

    private async Task<SmsGateClient?> CreateAuthenticatedClientAsync(SmsGateConnectionConfig config, bool includeWebhookScope = false)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var client = new SmsGateClient(httpClient)
            {
                BaseUrl = config.BaseUrl.TrimEnd('/') + "/api"
            };

            var scopes = new System.Collections.ObjectModel.Collection<JWTScope>
                { JWTScope.MessagesSend, JWTScope.MessagesList, JWTScope.DevicesList };
            if (includeWebhookScope)
                scopes.Add(JWTScope.WebhooksWrite);

            var tokenResponse = await client.TokenPOSTAsync(new TokenRequest
            {
                Ttl = 3600,
                Scopes = scopes
            });

            // Switch to Bearer auth with the new token
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenResponse.Access_token);

            return client;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate authentication failed");
            return null;
        }
    }
}
