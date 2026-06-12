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
        private readonly string _apiToken;
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public SmsProxyHubClient(HttpClient httpClient, string apiToken)
        {
            _httpClient = httpClient;
            _apiToken = apiToken;
        }

        /// <summary>
        /// Send an SMS. Payload gets echoed back on webhook reply.
        /// </summary>
        public Task<SendSmsResponse> SendSmsAsync(
            Guid? connectionId, string phoneNumber, string message,
            CancellationToken ct = default)
        {
            return SendSmsAsync<object>(connectionId, phoneNumber, message, null, ct);
        }

        /// <summary>
        /// Send an SMS with a typed payload that gets serialized and echoed back on webhook reply.
        /// </summary>
        public async Task<SendSmsResponse> SendSmsAsync<T>(
            Guid? connectionId, string phoneNumber, string message, T payload = default,
            CancellationToken ct = default)
        {
            var request = new SendSmsRequest
            {
                ConnectionId = connectionId,
                PhoneNumber = phoneNumber,
                Message = message,
                Payload = payload != null ? JsonConvert.SerializeObject(payload, JsonSettings) : null
            };

            var json = JsonConvert.SerializeObject(request, JsonSettings);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/messages/send") { Content = content })
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
                var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<SendSmsResponse>(body);
            }
        }
    }
}
