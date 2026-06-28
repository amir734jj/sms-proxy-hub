namespace Shared.Contracts;

// A single log line streamed to admin clients over SignalR.
// Source is "App" (Serilog) or "SmsGate" (polled from the SMS Gate server).
public record LogStreamEntry(
    string Source,
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? Detail = null);
