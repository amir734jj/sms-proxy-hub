namespace Shared.Contracts;

public record SmsConnectionDto(
    Guid Id,
    string Name,
    string ProviderType,
    bool IsActive,
    int Priority,
    DateTimeOffset CreatedAt);
