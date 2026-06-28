using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> TokenCache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SendThrottles = new();
    private static readonly TimeSpan ThrottleDelay = TimeSpan.FromMilliseconds(500);

    // SMS Gate validates the webhook Id with a `max=36` tag (see smsgateway.Webhook.id maxLength in
    // the OpenAPI spec). Exceeding it makes the registration POST fail with HTTP 400, which previously
    // happened silently. A bare GUID is exactly 36 chars, so any generated id must stay within this.
    private const int MaxWebhookIdLength = 36;

    public SmsProviderType ProviderType => SmsProviderType.SmsGate;

    public async Task<string?> SendAsync(string to, string message, SmsConnectionConfig config)
    {
        if (config is not SmsGateConnectionConfig smsGate)
        {
            logger.LogError("Invalid config type for SmsGate provider");
            return null;
        }

        // throttle per base URL to avoid overwhelming the SMS Gate server
        var throttleKey = smsGate.BaseUrl;
        var throttle = SendThrottles.GetOrAdd(throttleKey, _ => new SemaphoreSlim(1, 1));
        await throttle.WaitAsync();

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
        finally
        {
            // delay before allowing the next send to the same server
            await Task.Delay(ThrottleDelay);
            throttle.Release();
        }
    }

    public async Task<IncomingSms?> ParseWebhookAsync(HttpRequest request, SmsConnectionConfig config)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        JObject root;
        try
        {
            root = JObject.Parse(body);
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            logger.LogWarning(ex, "SmsGate webhook body is not valid JSON; ignoring. Body: {Body}", body);
            return null;
        }

        var eventType = root["event"]?.ToString();
        // Inbound replies arrive as sms:received or mms:received (when the user replies with a picture/MMS).
        if (eventType != "sms:received" && eventType != "mms:received")
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

        // MMS payloads may carry the text under a different field than SMS.
        var sender = payload["sender"]?.ToString() ?? payload["phoneNumber"]?.ToString() ?? "";
        var message = (payload["message"] ?? payload["text"] ?? payload["subject"])?.ToString()?.Trim() ?? "";

        return new IncomingSms(
            sender,
            message,
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

            await RegisterWebhooksInternalAsync(client, config, connectionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register SMS Gate webhook for connection {ConnectionId}", connectionId);
        }
    }

    // Re-registers the SMS and MMS webhooks and reports what is currently registered on the device,
    // so the user can diagnose why an inbound reply (e.g. an MMS) was not delivered.
    public async Task<WebhookRevalidationResult> RevalidateWebhooksAsync(SmsGateConnectionConfig config, Guid connectionId)
    {
        try
        {
            var client = await CreateAuthenticatedClientAsync(config, includeWebhookScope: true);
            if (client is null)
                return new WebhookRevalidationResult(false, "Failed to authenticate with the SMS Gate server.", []);

            await RegisterWebhooksInternalAsync(client, config, connectionId);

            var all = await client.WebhooksAllAsync();
            var ours = all
                .Where(w => !string.IsNullOrEmpty(w.Id) && w.Id!.StartsWith(connectionId.ToString("N"), StringComparison.Ordinal))
                .Select(w => new RegisteredWebhookDto(w.Id!, w.Event.ToString(), w.Url ?? ""))
                .ToList();

            return new WebhookRevalidationResult(
                true,
                $"Re-registered SMS and MMS webhooks. {ours.Count} webhook(s) currently active on the device.",
                ours);
        }
        catch (SmsGateApiException ex)
        {
            logger.LogWarning("SmsGate revalidate returned {StatusCode}: {Response}", ex.StatusCode, ex.Response);
            return new WebhookRevalidationResult(false, $"SMS Gate returned {ex.StatusCode}: {ex.Response}", []);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revalidate SMS Gate webhooks for connection {ConnectionId}", connectionId);
            return new WebhookRevalidationResult(false, ex.Message, []);
        }
    }

    // Lists every webhook currently registered on the SMS Gate device/account.
    public async Task<List<RegisteredWebhookDto>> GetRegisteredWebhooksAsync(SmsGateConnectionConfig config)
    {
        try
        {
            var client = await CreateAuthenticatedClientAsync(config, includeWebhookScope: true);
            if (client is null) return [];

            var all = await client.WebhooksAllAsync();
            return all
                .Select(w => new RegisteredWebhookDto(w.Id ?? "", w.Event.ToString(), w.Url ?? ""))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list SMS Gate webhooks");
            return [];
        }
    }

    // Fetches recent log entries from the SMS Gate server for the given connection.
    public async Task<List<LogEntry>> GetLogsAsync(SmsGateConnectionConfig config, DateTimeOffset? from = null)
    {
        try
        {
            var client = await CreateAuthenticatedClientAsync(config, includeLogsScope: true);
            if (client is null) return [];

            var logs = await client.LogsAsync(from, null);
            return logs.ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SmsGate fetch logs failed");
            return [];
        }
    }

    private async Task RegisterWebhooksInternalAsync(SmsGateClient client, SmsGateConnectionConfig config, Guid connectionId)
    {
        var webhookUrl = $"{configuration["App:PublicUrl"]!.TrimEnd('/')}/api/provider-webhook/{connectionId}";

        // SMS Gate limits the webhook Id to MaxWebhookIdLength (36) chars. Use the 32-char "N" GUID
        // format plus a short suffix so both the SMS and MMS ids stay within the limit.
        var idPrefix = connectionId.ToString("N");

        // Register one webhook per inbound event. MMS replies fire mms:received, which was previously
        // never registered, so picture/MMS replies were silently dropped.
        var subscriptions = new (string IdSuffix, WebhookEvent Event)[]
        {
            ("-sms", WebhookEvent.SmsReceived),
            ("-mms", WebhookEvent.MmsReceived)
        };

        foreach (var (idSuffix, webhookEvent) in subscriptions)
        {
            var webhookId = $"{idPrefix}{idSuffix}";

            // Guard against ever sending an id that SMS Gate would reject with a 400.
            if (webhookId.Length > MaxWebhookIdLength)
                throw new InvalidOperationException(
                    $"Webhook id '{webhookId}' is {webhookId.Length} chars, exceeding the SMS Gate limit of {MaxWebhookIdLength}.");

            await client.WebhooksPOSTAsync(new Webhook
            {
                Id = webhookId,
                Event = webhookEvent,
                Url = webhookUrl,
                DeviceId = config.DeviceId
            });

            logger.LogInformation("SMS Gate webhook registered: {WebhookId} ({Event}) -> {Url}",
                webhookId, webhookEvent, webhookUrl);
        }
    }

    private async Task<SmsGateClient?> CreateAuthenticatedClientAsync(SmsGateConnectionConfig config, bool includeWebhookScope = false, bool includeLogsScope = false)
    {
        try
        {
            // Tokens carrying extra scopes (webhooks/logs) are fetched fresh rather than cached.
            var extraScopes = includeWebhookScope || includeLogsScope;
            var cacheKey = $"{config.BaseUrl}|{config.Username}";
            var httpClient = httpClientFactory.CreateClient();

            // check token cache
            if (!extraScopes && TokenCache.TryGetValue(cacheKey, out var cached)
                && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", cached.Token);
                return new SmsGateClient(httpClient) { BaseUrl = config.BaseUrl.TrimEnd('/') + "/api" };
            }

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
            {
                scopes.Add(JWTScope.WebhooksWrite);
                scopes.Add(JWTScope.WebhooksList);
            }
            if (includeLogsScope)
                scopes.Add(JWTScope.LogsRead);

            var tokenResponse = await client.TokenPOSTAsync(new TokenRequest
            {
                Ttl = 3600,
                Scopes = scopes
            });

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenResponse.Access_token);

            // cache the token (not for webhook/logs-scoped tokens since those are one-off)
            if (!extraScopes)
            {
                var expiresAt = tokenResponse.Expires_at ?? DateTimeOffset.UtcNow.AddMinutes(50);
                // use token for 2/3 of its lifetime, then refresh
                var lifetime = expiresAt - DateTimeOffset.UtcNow;
                var cacheUntil = DateTimeOffset.UtcNow + (lifetime * 2 / 3);
                TokenCache[cacheKey] = (tokenResponse.Access_token, cacheUntil);
            }

            return client;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate authentication failed");
            return null;
        }
    }
}
