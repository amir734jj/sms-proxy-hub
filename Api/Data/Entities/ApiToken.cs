using Api.Interfaces;

namespace Api.Data.Entities;

/// <summary>
/// API tokens for programmatic access (e.g., xldent).
/// </summary>
public sealed class ApiToken : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
