namespace Shared.Contracts;

/// <summary>
/// ConnectionId is optional. If null, the system tries all active connections in priority order (lowest first).
/// </summary>
public record SendSmsRequest(Guid? ConnectionId, string PhoneNumber, string Message, string? Payload = null);

public record SendSmsResponse(string? MessageId, string Status, Guid? UsedConnectionId = null);
