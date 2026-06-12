using Api.Data.Entities;
using Shared.Contracts;

namespace Api.Interfaces;

public interface IMessageService
{
    Task<(string? MessageId, bool Success, Guid? UsedConnectionId)> SendAsync(Guid userId, Guid? connectionId, string phoneNumber, string message, string? payload);
    Task<List<SmsMessageDto>> GetAllForUserAsync(Guid userId);
    Task<List<SmsMessageDto>> GetByConnectionAsync(Guid connectionId);
    Task<SmsMessage?> FindLatestSentToPhoneAsync(Guid connectionId, string phone);
    Task MarkReplyReceivedAsync(Guid messageId);
    Task<UsageStatsDto> GetUsageForUserAsync(Guid userId, int days = 30);
}
