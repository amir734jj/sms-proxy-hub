using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace SmsProxyHub.Client;

/// <summary>
/// HTTP client for SmsProxyHub. Consumers only need an API token.
/// </summary>
public sealed class SmsProxyHubClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    public SmsProxyHubClient(HttpClient httpClient, string apiToken)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiToken);
    }

    /// <summary>
    /// Send an SMS through the proxy. Optionally include a payload that gets echoed back on webhook reply.
    /// </summary>
    public async Task<SendSmsResponse> SendSmsAsync(
        Guid connectionId, string phoneNumber, string message, string? payload = null,
        CancellationToken ct = default)
    {
        var request = new SendSmsRequest
        {
            ConnectionId = connectionId,
            PhoneNumber = phoneNumber,
            Message = message,
            Payload = payload
        };

        var json = JsonConvert.SerializeObject(request, JsonSettings);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/messages/send", content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonConvert.DeserializeObject<SendSmsResponse>(body)!;
    }
}
