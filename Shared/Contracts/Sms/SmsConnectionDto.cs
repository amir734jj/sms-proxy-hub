namespace Shared.Contracts;

public record SmsConnectionDto(
    Guid Id,
    string Name,
    SmsProviderType ProviderType,
    bool IsActive,
    int Priority,
    int MessageRetentionDays,
    DateTimeOffset CreatedAt);

public record SmsGateDeviceDto(string Id, string Name, string? LastSeen);

public record WebhookRevalidationResult(
    bool Success,
    string Message,
    List<RegisteredWebhookDto> RegisteredWebhooks);

public record RegisteredWebhookDto(string Id, string Event, string Url);

