using Api.Data.Entities;
using Api.Hubs;
using Api.Providers;
using EfCoreRepository.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Shared.Contracts;

namespace Api.Workers;

// Polls the SMS Gate server logs for every SmsGate connection and streams new
// entries to admins watching the LogsHub. Only runs while at least one admin is connected.
public sealed class SmsGateLogStreamWorker(
    IServiceScopeFactory scopeFactory,
    SmsGateProvider smsGateProvider,
    IHubContext<LogsHub> hub,
    ILogger<SmsGateLogStreamWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerSettings JsonSettings = new() { TypeNameHandling = TypeNameHandling.None };

    // Dedup key = "{connectionId}:{logEntryId}". Cleared when it grows too large.
    private readonly HashSet<string> _seen = [];
    private DateTimeOffset _since = DateTimeOffset.UtcNow.AddMinutes(-1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);

                if (LogsHub.ClientCount == 0)
                {
                    // Nobody watching: reset the window so we don't replay old logs on reconnect.
                    _since = DateTimeOffset.UtcNow.AddMinutes(-1);
                    _seen.Clear();
                    continue;
                }

                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SMS Gate log poll failed");
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEfRepository>();

        var connections = (await repository.For<SmsConnection>().GetAll<SmsConnection>(
            filterExprs: [c => c.ProviderType == SmsProviderType.SmsGate])).ToList();

        // Overlap the window slightly so entries on the boundary aren't missed; dedup handles repeats.
        var from = _since.AddSeconds(-PollInterval.TotalSeconds);
        var newest = _since;

        foreach (var connection in connections)
        {
            var config = JsonConvert.DeserializeObject<SmsConnectionConfig>(connection.ConfigJson, JsonSettings);
            if (config is not SmsGateConnectionConfig smsGate) continue;

            var entries = await smsGateProvider.GetLogsAsync(smsGate, from);
            foreach (var entry in entries.OrderBy(e => e.CreatedAt))
            {
                var key = $"{connection.Id}:{entry.Id}";
                if (!_seen.Add(key)) continue;

                var timestamp = entry.CreatedAt ?? DateTimeOffset.UtcNow;
                if (timestamp > newest) newest = timestamp;

                var message = string.IsNullOrWhiteSpace(entry.Module)
                    ? entry.Message ?? ""
                    : $"[{connection.Name}/{entry.Module}] {entry.Message}";

                var dto = new LogStreamEntry(
                    "SmsGate",
                    timestamp,
                    entry.Priority?.ToString() ?? "INFO",
                    message,
                    entry.Context is { Count: > 0 } ? JsonConvert.SerializeObject(entry.Context) : null);

                await hub.Clients.Group(LogsHub.GroupName).SendAsync("Log", dto, ct);
            }
        }

        _since = newest;

        // Keep the dedup set bounded.
        if (_seen.Count > 5000) _seen.Clear();
    }
}
