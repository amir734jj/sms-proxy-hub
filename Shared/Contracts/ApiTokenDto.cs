namespace Shared.Contracts;

/// <summary>
/// API token for programmatic access (e.g., from xldent or other consumers).
/// </summary>
public record ApiTokenDto(Guid Id, string Token, string Name, bool IsActive, DateTimeOffset CreatedAt);

public record CreateApiTokenRequest(string Name);
