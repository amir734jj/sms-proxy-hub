using System;
using System.Net;

namespace SmsProxyHub.Client
{
    /// <summary>
    /// Thrown when the SmsProxyHub API returns a non-success status code.
    /// Exposes the HTTP status code and the raw response body so callers can
    /// see why the request was rejected (e.g. "Message too long").
    /// </summary>
    public sealed class SmsProxyHubException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public string ResponseBody { get; }

        public SmsProxyHubException(HttpStatusCode statusCode, string responseBody)
            : base($"SmsProxyHub request failed with status {(int)statusCode} ({statusCode}): {responseBody}")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
