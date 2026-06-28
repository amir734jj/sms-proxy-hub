namespace Shared.Contracts;

// Live device liveness derived from inbound provider webhooks (e.g. SMS Gate system:ping).
public record DeviceStatusDto(
    bool Online,
    DateTimeOffset? LastSeenAt,
    int? BatteryLevel,
    bool? IsCharging,
    bool? HasInternet,
    int? ConnectionTransport,
    int? CellularNetworkType,
    int? FailedMessagesLastHour,
    string? DeviceId,
    string? PingId,
    string? WebhookId,
    string? HealthStatus,
    int? ReleaseId,
    string? Version,
    IReadOnlyList<DeviceHealthCheckDto> Checks);

public record DeviceHealthCheckDto(
    string Key,
    string? Description,
    string? ObservedUnit,
    int? ObservedValue,
    string? Status);

