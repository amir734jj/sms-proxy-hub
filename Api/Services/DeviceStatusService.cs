using System.Collections.Concurrent;
using Api.Generated.SmsGate;
using Api.Interfaces;
using Newtonsoft.Json;
using Shared.Contracts;

namespace Api.Services;

// In-memory device liveness derived from inbound provider webhooks. SMS Gate's system:ping fires
// roughly every 60s, so a device that pinged within OnlineWindow is considered online. State is
// not persisted: it resets on restart and repopulates from the next ping.
public sealed class DeviceStatusService : IDeviceStatusService
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(3);
    private readonly ILogger<DeviceStatusService> _logger;

    private sealed class Status
    {
        public DateTimeOffset LastSeenAt;
        public int? BatteryLevel;
        public bool? IsCharging;
        public bool? HasInternet;
        public int? ConnectionTransport;
        public int? CellularNetworkType;
        public int? FailedMessagesLastHour;
        public string? DeviceId;
        public string? PingId;
        public string? WebhookId;
        public string? HealthStatus;
        public int? ReleaseId;
        public string? Version;
        public IReadOnlyList<DeviceHealthCheckDto> Checks = [];
    }

    private sealed class PingWebhookEnvelope
    {
        [JsonProperty("deviceId")]
        public string? DeviceId { get; set; }

        [JsonProperty("event")]
        public string? Event { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("webhookId")]
        public string? WebhookId { get; set; }

        [JsonProperty("payload")]
        public PingWebhookPayload? Payload { get; set; }
    }

    private sealed class PingWebhookPayload
    {
        [JsonProperty("health")]
        public HealthResponse? Health { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, Status> _statuses = new();

    public DeviceStatusService(ILogger<DeviceStatusService> logger)
    {
        _logger = logger;
    }

    public void Record(Guid connectionId, string? rawBody)
    {
        var status = _statuses.GetOrAdd(connectionId, _ => new Status());
        status.LastSeenAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(rawBody)) return;

        _logger.LogDebug("Incoming provider webhook raw body for {ConnectionId}: {RawBody}", connectionId, rawBody);

        // Best-effort: pull health metadata from an SMS Gate system:ping payload.
        // Non-JSON bodies (e.g. Twilio form posts) or unexpected shapes are ignored.
        try
        {
            var envelope = JsonConvert.DeserializeObject<PingWebhookEnvelope>(rawBody);
            if (envelope is null || !string.Equals(envelope.Event, "system:ping", StringComparison.OrdinalIgnoreCase)) return;

            status.DeviceId = envelope.DeviceId;
            status.PingId = envelope.Id;
            status.WebhookId = envelope.WebhookId;

            var health = envelope.Payload?.Health;
            if (health is null) return;

            status.ReleaseId = health.ReleaseId;
            status.Version = health.Version;
            status.HealthStatus = ToStatusString(health.Status);

            var checks = health.Checks;
            if (checks is null || checks.Count == 0) return;

            status.Checks = checks
                .Select(kvp => new DeviceHealthCheckDto(
                    kvp.Key,
                    kvp.Value.Description,
                    kvp.Value.ObservedUnit,
                    kvp.Value.ObservedValue,
                    ToStatusString(kvp.Value.Status)))
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            status.BatteryLevel = GetObservedValue(checks, "battery:level");

            var charging = GetObservedValue(checks, "battery:charging");
            if (charging is not null)
            {
                // SMS Gate reports charging as a bit-flag value where any non-zero means charging.
                status.IsCharging = charging != 0;
            }

            var internet = GetObservedValue(checks, "connection:status");
            if (internet is not null)
            {
                status.HasInternet = internet != 0;
            }

            status.ConnectionTransport = GetObservedValue(checks, "connection:transport");
            status.CellularNetworkType = GetObservedValue(checks, "connection:cellular");
            status.FailedMessagesLastHour = GetObservedValue(checks, "messages:failed");
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
        return new DeviceStatusDto(
            online,
            status.LastSeenAt,
            status.BatteryLevel,
            status.IsCharging,
            status.HasInternet,
            status.ConnectionTransport,
            status.CellularNetworkType,
            status.FailedMessagesLastHour,
            status.DeviceId,
            status.PingId,
            status.WebhookId,
            status.HealthStatus,
            status.ReleaseId,
            status.Version,
            status.Checks);
    }

    private static int? GetObservedValue(HealthChecks checks, string key)
    {
        if (!checks.TryGetValue(key, out var check) || check is null)
        {
            return null;
        }

        return check.ObservedValue;
    }

    private static string? ToStatusString(HealthStatus? status)
    {
        return status?.ToString().ToLowerInvariant();
    }
}
