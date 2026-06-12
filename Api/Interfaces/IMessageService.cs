using Api.Data.Entities;
using Shared.Contracts;

namespace Api.Interfaces;

public interface IMessageService
{
    Task<(string? MessageId, bool Success)> SendAsync(Guid connectionId, string phoneNumber, string message, string? payload);
    Task<List<SmsMessageDto>> GetAllForUserAsync(Guid userId);
    Task<List<SmsMessageDto>> GetByConnectionAsync(Guid connectionId);
    Task<SmsMessage?> FindLatestSentToPhoneAsync(Guid connectionId, string phone);
    Task MarkReplyReceivedAsync(Guid messageId);
}
