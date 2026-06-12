namespace Shared.Contracts;

public record SmsConnectionDto(
    Guid Id,
    string Name,
    SmsProviderType ProviderType,
    bool IsActive,
    int Priority,
    DateTimeOffset CreatedAt);

public record SmsGateDeviceDto(string Id, string Name, string? LastSeen);
