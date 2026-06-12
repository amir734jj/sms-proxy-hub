namespace Shared.Contracts;

public record SendSmsRequest(Guid ConnectionId, string PhoneNumber, string Message, string? Payload = null);

public record SendSmsResponse(string? MessageId, string Status);
