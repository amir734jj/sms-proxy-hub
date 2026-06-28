using System.Collections.Concurrent;
using Api.Interfaces;
using Newtonsoft.Json.Linq;
using Shared.Contracts;

namespace Api.Services;

// In-memory device liveness derived from inbound provider webhooks. SMS Gate's system:ping fires
// roughly every 60s, so a device that pinged within OnlineWindow is considered online. State is
// not persisted: it resets on restart and repopulates from the next ping.
public sealed class DeviceStatusService : IDeviceStatusService
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(3);

    private sealed class Status
    {
        public DateTimeOffset LastSeenAt;
        public int? BatteryLevel;
        public bool? IsCharging;
    }

    private readonly ConcurrentDictionary<Guid, Status> _statuses = new();

    public void Record(Guid connectionId, string? rawBody)
    {
        var status = _statuses.GetOrAdd(connectionId, _ => new Status());
        status.LastSeenAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(rawBody)) return;

        // Best-effort: pull battery/charging from an SMS Gate system:ping health payload.
        // Non-JSON bodies (e.g. Twilio form posts) or unexpected shapes are ignored.
        try
        {
            var root = JObject.Parse(rawBody);
            if (root["event"]?.ToString() != "system:ping") return;

            var checks = root["payload"]?["health"]?["checks"];
            if (checks is null) return;

            status.BatteryLevel = (int?)checks["battery:level"]?["observedValue"];
            var charging = (int?)checks["battery:charging"]?["observedValue"];
            if (charging is not null) status.IsCharging = charging == 1;
        }
        catch
        {
            // ignore non-JSON / unexpected shapes
        }
    }

    public DeviceStatusDto? Get(Guid connectionId)
    {
        if (!_statuses.TryGetValue(connectionId, out var status)) return null;

        var online = DateTimeOffset.UtcNow - status.LastSeenAt < OnlineWindow;
        return new DeviceStatusDto(online, status.LastSeenAt, status.BatteryLevel, status.IsCharging);
    }
}
