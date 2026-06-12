using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SmsProxyHub.Client
{
    /// <summary>
    /// HTTP client for SmsProxyHub. Consumers only need an API token.
    /// </summary>
    public sealed class SmsProxyHubClient
    {
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
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
        /// Send an SMS through the proxy. If connectionId is null, tries all connections in priority order (failover).
        /// Optionally include a payload that gets echoed back on webhook reply.
        /// </summary>
        public async Task<SendSmsResponse> SendSmsAsync(
            Guid? connectionId, string phoneNumber, string message, object payload = null,
            CancellationToken ct = default)
        {
            var request = new SendSmsRequest
            {
                ConnectionId = connectionId,
                PhoneNumber = phoneNumber,
                Message = message,
                Payload = payload is string s ? s : payload != null ? JsonConvert.SerializeObject(payload, JsonSettings) : null
            };

            var json = JsonConvert.SerializeObject(request, JsonSettings);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = await _httpClient.PostAsync("/api/messages/send", content, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<SendSmsResponse>(body);
            }
        }
    }
}
