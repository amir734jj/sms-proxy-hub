namespace Shared.Contracts;

// Live device liveness derived from inbound provider webhooks (e.g. SMS Gate system:ping).
public record DeviceStatusDto(
    bool Online,
    DateTimeOffset? LastSeenAt,
    int? BatteryLevel,
    bool? IsCharging);
