using Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Shared.Contracts;

namespace Api.Logging;

// Serilog sink that pushes each log event to admins connected to the LogsHub.
// The IHubContext is assigned after the DI container is built (HubContext setter).
public sealed class SignalRLogSink(IFormatProvider? formatProvider) : ILogEventSink
{
    public static IHubContext<LogsHub>? HubContext { get; set; }

    public void Emit(LogEvent logEvent)
    {
        var hub = HubContext;
        if (hub is null || LogsHub.ClientCount == 0)
            return;

        var entry = new LogStreamEntry(
            "App",
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            logEvent.RenderMessage(formatProvider),
            logEvent.Exception?.ToString());

        // Fire-and-forget; never let log streaming break the request path.
        _ = SafeSendAsync(hub, entry);
    }

    private static async Task SafeSendAsync(IHubContext<LogsHub> hub, LogStreamEntry entry)
    {
        try
        {
            await hub.Clients.Group(LogsHub.GroupName).SendAsync("Log", entry);
        }
        catch
        {
            // Ignore streaming failures.
        }
    }
}

public static class SignalRLogSinkExtensions
{
    public static LoggerConfiguration SignalR(
        this LoggerSinkConfiguration sinkConfiguration,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information,
        IFormatProvider? formatProvider = null)
    {
        return sinkConfiguration.Sink(new SignalRLogSink(formatProvider), restrictedToMinimumLevel);
    }
}
