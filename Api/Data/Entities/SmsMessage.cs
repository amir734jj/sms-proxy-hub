using Api.Interfaces;
using Shared.Contracts;

namespace Api.Data.Entities;

/// <summary>
/// Tracks every SMS sent through the proxy. Used for webhook reply correlation.
/// </summary>
public sealed class SmsMessage : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionId { get; set; }
    public string To { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Opaque JSON payload from the caller, echoed back in the webhook callback.
    /// </summary>
    public string? Payload { get; set; }

    public SmsMessageStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
