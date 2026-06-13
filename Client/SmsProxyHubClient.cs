using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmsProxyHub.Contracts;

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
        /// Send an SMS to one or more phone numbers.
        /// </summary>
        public Task<BulkSendSmsResponse> SendSmsAsync(
            Guid? connectionId, string[] phoneNumbers, string message,
            CancellationToken ct = default)
        {
            return SendSmsAsync<object>(connectionId, phoneNumbers, message, null, ct);
        }

        /// <summary>
        /// Send an SMS to one or more phone numbers with a typed payload.
        /// </summary>
        public async Task<BulkSendSmsResponse> SendSmsAsync<T>(
            Guid? connectionId, string[] phoneNumbers, string message, T payload = default,
            CancellationToken ct = default)
        {
            var request = new SendSmsRequest
            {
                ConnectionId = connectionId,
                PhoneNumbers = phoneNumbers,
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
                return JsonConvert.DeserializeObject<BulkSendSmsResponse>(body);
            }
        }
    }
}
