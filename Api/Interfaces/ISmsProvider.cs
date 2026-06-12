using Shared.Contracts;

namespace Api.Interfaces;

/// <summary>
/// Interface that all SMS providers must implement.
/// Add a new provider by implementing this interface and registering it in DI.
/// </summary>
public interface ISmsProvider
{
    /// <summary>
    /// Provider type identifier (e.g., "smsgate", "twilio"). Must match <see cref="SmsConnectionConfig.Type"/>.
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Send an SMS through this provider.
    /// </summary>
    /// <returns>Provider message ID on success, null on failure.</returns>
    Task<string?> SendAsync(string to, string message, SmsConnectionConfig config);

    /// <summary>
    /// Parse an incoming webhook request from this provider.
    /// </summary>
    /// <returns>Parsed incoming SMS or null if the request is not a valid SMS event.</returns>
    Task<IncomingSms?> ParseWebhookAsync(HttpRequest request, SmsConnectionConfig config);
}

public sealed record IncomingSms(string FromPhone, string Message, string? ProviderMessageId);
