namespace Shared.Contracts;

public class SmsMessageDto
{
    public Guid Id { get; init; }
    public Guid ConnectionId { get; init; }
    public string To { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ProviderMessageId { get; init; }
    public string? Payload { get; init; }
    public SmsMessageStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
