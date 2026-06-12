using Api.Interfaces;

namespace Api.Data.Entities;

/// <summary>
/// An SMS provider connection (SmsGate, Twilio, etc).
/// Config is stored as polymorphic JSON using JsonSubTypes — no migration needed for new providers.
/// </summary>
public sealed class SmsConnection : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
