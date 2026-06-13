namespace Shared.Contracts;

public record SendSmsRequest(Guid? ConnectionId, string[] PhoneNumbers, string Message, string? Payload = null);

public record SendSmsResponse(string? MessageId, string Status, Guid? UsedConnectionId = null);

public record BulkSendSmsResponse(List<SendSmsResponse> Results);
