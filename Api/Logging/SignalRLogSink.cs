using Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Shared.Contracts;

namespace Api.Logging;

// Serilog sink that pushes each log event to admins connected to the LogsHub.
public sealed class SignalRLogSink(IHubContext<LogsHub> hubContext, IFormatProvider? formatProvider) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        if (LogsHub.ClientCount == 0)
            return;

        var entry = new LogStreamEntry(
            "App",
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            logEvent.RenderMessage(formatProvider),
            logEvent.Exception?.ToString());

        // Fire-and-forget; never let log streaming break the request path.
        _ = SafeSendAsync(hubContext, entry);
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
        IHubContext<LogsHub> hubContext,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information,
        IFormatProvider? formatProvider = null)
    {
        return sinkConfiguration.Sink(new SignalRLogSink(hubContext, formatProvider), restrictedToMinimumLevel);
    }
}
