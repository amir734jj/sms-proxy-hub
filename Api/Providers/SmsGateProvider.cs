using System.Net.Http.Headers;
using System.Text;
using Api.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Contracts;

namespace Api.Providers;

public sealed class SmsGateProvider(IHttpClientFactory httpClientFactory, ILogger<SmsGateProvider> logger) : ISmsProvider
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
            var token = await GetAccessTokenAsync(smsGate);
            if (token is null) return null;

            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                phoneNumbers = new[] { to },
                textMessage = new { text = message }
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{smsGate.BaseUrl.TrimEnd('/')}/api/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("SmsGate returned {StatusCode}", response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);
            return result["id"]?.ToString();
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

        var webhook = JObject.Parse(body);
        if (webhook["event"]?.ToString() != "sms:received")
            return null;

        var payload = webhook["payload"];
        if (payload is null)
            return null;

        return new IncomingSms(
            payload["sender"]?.ToString() ?? string.Empty,
            payload["message"]?.ToString().Trim() ?? string.Empty,
            payload["messageId"]?.ToString());
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
            var json = JsonConvert.SerializeObject(tokenPayload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"{config.BaseUrl.TrimEnd('/')}/api/token", content);

            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);
            return result["access_token"]?.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmsGate authentication failed");
            return null;
        }
    }
}
