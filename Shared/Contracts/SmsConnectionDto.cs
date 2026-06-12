namespace Shared.Contracts;

public record SmsConnectionDto(
    Guid Id,
    string Name,
    string ProviderType,
    bool IsActive,
    DateTimeOffset CreatedAt);
