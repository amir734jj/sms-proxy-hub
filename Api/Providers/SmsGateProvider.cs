using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Interfaces;
using Shared.Contracts;

namespace Api.Providers;

public sealed class SmsGateProvider(IHttpClientFactory httpClientFactory, ILogger<SmsGateProvider> logger) : ISmsProvider
{
    public string ProviderType => "smsgate";

    public async Task<string?> SendAsync(string to, string message, SmsConnectionConfig config)
    {
        if (config is not SmsGateConnectionConfig smsGate)
        {
            logger.LogError("Invalid config type for SmsGate provider");
            return null;
        }

        try
        {
            var token = await GetAccessTokenAsync(smsGate);
            if (token is null) return null;

            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                phoneNumbers = new[] { to },
                textMessage = new { text = message }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{smsGate.BaseUrl.TrimEnd('/')}/api/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("SmsGate returned {StatusCode}", response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SmsGateSendResponse>(body);
            return result?.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate send failed");
            return null;
        }
    }

    public async Task<IncomingSms?> ParseWebhookAsync(HttpRequest request, SmsConnectionConfig config)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        var webhook = JsonSerializer.Deserialize<SmsGateWebhookPayload>(body);
        if (webhook?.Event != "sms:received" || webhook.Payload is null)
            return null;

        return new IncomingSms(
            webhook.Payload.Sender ?? string.Empty,
            webhook.Payload.Message?.Trim() ?? string.Empty,
            webhook.Payload.MessageId);
    }

    private async Task<string?> GetAccessTokenAsync(SmsGateConnectionConfig config)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var tokenPayload = new { ttl = 3600, scopes = new[] { "messages:send", "messages:list" } };
            var json = JsonSerializer.Serialize(tokenPayload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"{config.BaseUrl.TrimEnd('/')}/api/token", content);

            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var tokenResult = JsonSerializer.Deserialize<SmsGateTokenResponse>(body);
            return tokenResult?.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate authentication failed");
            return null;
        }
    }

    private sealed class SmsGateSendResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private sealed class SmsGateTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class SmsGateWebhookPayload
    {
        [JsonPropertyName("event")]
        public string? Event { get; set; }

        [JsonPropertyName("payload")]
        public SmsGateWebhookMessage? Payload { get; set; }
    }

    private sealed class SmsGateWebhookMessage
    {
        [JsonPropertyName("messageId")]
        public string? MessageId { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("sender")]
        public string? Sender { get; set; }
    }
}
