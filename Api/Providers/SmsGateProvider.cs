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
    // Cached access token plus the refresh token used to renew it and the time we should renew at.
    private sealed record TokenCacheEntry(string AccessToken, string? RefreshToken, DateTimeOffset RefreshAt);

    private static readonly ConcurrentDictionary<string, TokenCacheEntry> TokenCache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TokenLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SendThrottles = new();
    private static readonly TimeSpan ThrottleDelay = TimeSpan.FromMilliseconds(500);

    // Base URLs whose server returned 501 for the logs endpoint; we stop polling those.
    private static readonly ConcurrentDictionary<string, byte> LogsUnsupported = new();

    // Base URLs whose server returned 501 for token refresh; we fall back to Basic auth for those.
    private static readonly ConcurrentDictionary<string, byte> RefreshUnsupported = new();

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
    // Returns false (via LogsSupported) when the server doesn't implement the logs API (HTTP 501),
    // so callers can stop polling it.
    public async Task<List<LogEntry>> GetLogsAsync(SmsGateConnectionConfig config, DateTimeOffset? from = null)
    {
        // Some SMS Gate server builds don't implement the logs endpoint; skip those we've already seen.
        if (LogsUnsupported.ContainsKey(config.BaseUrl))
            return [];

        try
        {
            var client = await CreateAuthenticatedClientAsync(config, includeLogsScope: true);
            if (client is null) return [];

            var logs = await client.LogsAsync(from, null);
            return logs.ToList();
        }
        catch (SmsGateApiException ex) when (ex.StatusCode == 501)
        {
            // Log once, then stop hitting this server's logs endpoint.
            if (LogsUnsupported.TryAdd(config.BaseUrl, 0))
                logger.LogInformation("SMS Gate server {BaseUrl} does not implement the logs API; log streaming disabled for it.", config.BaseUrl);
            return [];
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

    // Returns an authenticated SmsGateClient, preferring a cached access token, then a refresh-token
    // renewal, and finally a full Basic-credential re-authentication. Webhook/logs scopes are one-off.
    private async Task<SmsGateClient?> CreateAuthenticatedClientAsync(SmsGateConnectionConfig config, bool includeWebhookScope = false, bool includeLogsScope = false)
    {
        try
        {
            var extraScopes = includeWebhookScope || includeLogsScope;
            var cacheKey = $"{config.BaseUrl}|{config.Username}";
            var apiBaseUrl = config.BaseUrl.TrimEnd('/') + "/api";

            // One-off scoped tokens (webhooks/logs) are never cached or refreshed.
            if (extraScopes)
            {
                var (scopedClient, _) = await AuthenticateWithBasicAsync(config, apiBaseUrl, BuildScopes(includeWebhookScope, includeLogsScope));
                return scopedClient;
            }

            // Fast path: reuse a still-fresh cached access token without locking.
            if (TryGetFreshAccessToken(cacheKey, out var fastToken))
                return BuildBearerClient(fastToken, apiBaseUrl);

            // Single-flight: only one (re)authentication per connection at a time, so concurrent
            // callers don't all mint or refresh separate tokens when the cache is stale.
            var gate = TokenLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                // Another caller may have renewed the token while we waited on the lock.
                if (TryGetFreshAccessToken(cacheKey, out var token))
                    return BuildBearerClient(token, apiBaseUrl);

                var renewed = await RenewAsync(config, cacheKey, apiBaseUrl);
                return renewed is null ? null : BuildBearerClient(renewed, apiBaseUrl);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate authentication failed");
            return null;
        }
    }

    // Renews the cached base-scope token: tries a refresh-token renewal first, then Basic re-auth.
    // Stores the result and returns the new access token (or null on failure).
    private async Task<string?> RenewAsync(SmsGateConnectionConfig config, string cacheKey, string apiBaseUrl)
    {
        // Try a refresh first - it's cheaper and doesn't resend the Basic credentials.
        if (TokenCache.TryGetValue(cacheKey, out var existing)
            && !string.IsNullOrEmpty(existing.RefreshToken)
            && !RefreshUnsupported.ContainsKey(config.BaseUrl))
        {
            var refreshed = await TryRefreshAsync(config.BaseUrl, existing.RefreshToken!, apiBaseUrl);
            if (refreshed is not null)
            {
                StoreToken(cacheKey, refreshed);
                return refreshed.Access_token;
            }
            // Refresh failed/unsupported - fall through to a full re-authentication.
        }

        var (_, tokenResponse) = await AuthenticateWithBasicAsync(config, apiBaseUrl, BuildScopes(false, false));
        StoreToken(cacheKey, tokenResponse);
        return tokenResponse.Access_token;
    }

    // Calls the refresh endpoint with the refresh token as the bearer credential.
    // Returns null (and falls back to Basic) on 501/401/403 or any error.
    private async Task<TokenResponse?> TryRefreshAsync(string baseUrl, string refreshToken, string apiBaseUrl)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
            var client = new SmsGateClient(httpClient) { BaseUrl = apiBaseUrl };
            return await client.RefreshAsync();
        }
        catch (SmsGateApiException ex) when (ex.StatusCode == 501)
        {
            if (RefreshUnsupported.TryAdd(baseUrl, 0))
                logger.LogInformation("SMS Gate server {BaseUrl} does not implement token refresh; using Basic re-auth.", baseUrl);
            return null;
        }
        catch (SmsGateApiException ex) when (ex.StatusCode is 401 or 403)
        {
            // Refresh token expired or revoked; re-authenticate with credentials.
            logger.LogInformation("SMS Gate refresh token rejected ({StatusCode}); re-authenticating with credentials.", ex.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMS Gate token refresh failed; falling back to Basic auth.");
            return null;
        }
    }

    // Full authentication with Basic credentials. Returns a client already switched to the new
    // access token, plus the raw token response (so the caller can cache it).
    private async Task<(SmsGateClient Client, TokenResponse Token)> AuthenticateWithBasicAsync(
        SmsGateConnectionConfig config, string apiBaseUrl, System.Collections.ObjectModel.Collection<JWTScope> scopes)
    {
        var httpClient = httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var client = new SmsGateClient(httpClient) { BaseUrl = apiBaseUrl };

        var token = await client.TokenPOSTAsync(new TokenRequest { Ttl = 3600, Scopes = scopes });

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Access_token);
        return (client, token);
    }

    // Caches the token, scheduling renewal after 2/3 of its lifetime. Skips caching for
    // non-positive lifetimes (clock skew / already expired).
    private static void StoreToken(string cacheKey, TokenResponse token)
    {
        var expiresAt = token.Expires_at ?? DateTimeOffset.UtcNow.AddMinutes(50);
        var lifetime = expiresAt - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero) return;

        var refreshAt = DateTimeOffset.UtcNow + (lifetime * 2 / 3);
        TokenCache[cacheKey] = new TokenCacheEntry(token.Access_token, token.Refresh_token, refreshAt);
    }

    private static bool TryGetFreshAccessToken(string cacheKey, out string accessToken)
    {
        if (TokenCache.TryGetValue(cacheKey, out var entry) && DateTimeOffset.UtcNow < entry.RefreshAt)
        {
            accessToken = entry.AccessToken;
            return true;
        }

        accessToken = string.Empty;
        return false;
    }

    private SmsGateClient BuildBearerClient(string bearerToken, string apiBaseUrl)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return new SmsGateClient(httpClient) { BaseUrl = apiBaseUrl };
    }

    private static System.Collections.ObjectModel.Collection<JWTScope> BuildScopes(bool includeWebhookScope, bool includeLogsScope)
    {
        var scopes = new System.Collections.ObjectModel.Collection<JWTScope>
            { JWTScope.MessagesSend, JWTScope.MessagesList, JWTScope.DevicesList };
        if (includeWebhookScope)
        {
            scopes.Add(JWTScope.WebhooksWrite);
            scopes.Add(JWTScope.WebhooksList);
        }
        if (includeLogsScope)
            scopes.Add(JWTScope.LogsRead);
        return scopes;
    }
}
