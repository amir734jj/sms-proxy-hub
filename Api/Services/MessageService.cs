using Api.Data.Entities;
using Api.Interfaces;
using EfCoreRepository.Interfaces;
using EfCoreRepository.Extensions;
using Newtonsoft.Json;
using Shared;
using Shared.Contracts;

namespace Api.Services;

public sealed class MessageService(
    IEfRepository repository,
    IConnectionService connectionService,
    ISmsProviderFactory providerFactory,
    ILogger<MessageService> logger) : IMessageService
{
    private IBasicCrud<SmsMessage> Dal => repository.For<SmsMessage>();

    public async Task<(string? MessageId, bool Success, Guid? UsedConnectionId)> SendAsync(
        Guid userId, Guid? connectionId, string phoneNumber, string message, string? payload)
    {
        var normalizedPhone = PhoneUtility.NormalizePhoneNumber(phoneNumber) ?? phoneNumber;

        // If a specific connection is requested, try only that one
        if (connectionId is not null)
        {
            var connection = await connectionService.GetByIdAsync(connectionId.Value);
            if (connection is null || !connection.IsActive || connection.UserId != userId)
            {
                logger.LogWarning("Connection {ConnectionId} not found, inactive, or not owned by user", connectionId);
                return (null, false, null);
            }

            var (msgId, ok) = await TrySendViaConnection(connection, normalizedPhone, message, payload);
            return (msgId, ok, ok ? connection.Id : null);
        }

        // No specific connection — try all active connections in priority order
        var connections = await connectionService.GetActiveForUserInPriorityOrderAsync(userId);
        if (connections.Count == 0)
        {
            logger.LogWarning("No active connections for user {UserId}", userId);
            return (null, false, null);
        }

        foreach (var conn in connections)
        {
            var (msgId, ok) = await TrySendViaConnection(conn, normalizedPhone, message, payload);
            if (ok)
            {
                return (msgId, true, conn.Id);
            }

            logger.LogWarning("Connection {ConnectionName} (priority {Priority}) failed, trying next",
                conn.Name, conn.Priority);
        }

        logger.LogError("All {Count} connections failed for user {UserId}", connections.Count, userId);
        return (null, false, null);
    }

    private async Task<(string? MessageId, bool Success)> TrySendViaConnection(
        SmsConnection connection, string normalizedPhone, string message, string? payload)
    {
        var config = JsonConvert.DeserializeObject<SmsConnectionConfig>(connection.ConfigJson);
        if (config is null)
        {
            logger.LogError("Failed to deserialize config for connection {ConnectionId}", connection.Id);
            await Dal.Save(new SmsMessage
            {
                ConnectionId = connection.Id,
                To = normalizedPhone,
                Message = message,
                Payload = payload,
                Status = SmsMessageStatus.Failed
            });
            return (null, false);
        }

        var provider = providerFactory.GetProvider(connection.ProviderType);
        var messageId = await provider.SendAsync(normalizedPhone, message, config);

        await Dal.Save(new SmsMessage
        {
            ConnectionId = connection.Id,
            To = normalizedPhone,
            Message = message,
            ProviderMessageId = messageId,
            Payload = payload,
            Status = messageId is not null ? SmsMessageStatus.Sent : SmsMessageStatus.Failed
        });

        return (messageId, messageId is not null);
    }

    public async Task<List<SmsMessageDto>> GetAllForUserAsync(Guid userId)
    {
        // Get user's connection IDs first
        var connections = await connectionService.GetAllForUserAsync(userId);
        var connectionIds = connections.Select(c => c.Id).ToList();

        if (connectionIds.Count == 0) return [];

        return (await Dal.GetAll(
            filterExprs: [m => connectionIds.Contains(m.ConnectionId)],
            orderByDesc: m => m.CreatedAt,
            project: m => new SmsMessageDto
            {
                Id = m.Id,
                ConnectionId = m.ConnectionId,
                To = m.To,
                Message = m.Message,
                ProviderMessageId = m.ProviderMessageId,
                Payload = m.Payload,
                Status = m.Status,
                CreatedAt = m.CreatedAt
            })).ToList();
    }

    public async Task<List<SmsMessageDto>> GetByConnectionAsync(Guid connectionId)
    {
        return (await Dal.GetAll(
            filterExprs: [m => m.ConnectionId == connectionId],
            orderByDesc: m => m.CreatedAt,
            project: m => new SmsMessageDto
            {
                Id = m.Id,
                ConnectionId = m.ConnectionId,
                To = m.To,
                Message = m.Message,
                ProviderMessageId = m.ProviderMessageId,
                Payload = m.Payload,
                Status = m.Status,
                CreatedAt = m.CreatedAt
            })).ToList();
    }

    public async Task<SmsMessage?> FindLatestSentToPhoneAsync(Guid connectionId, string phone)
    {
        var normalized = PhoneUtility.NormalizePhoneNumber(phone) ?? phone;
        var candidates = new HashSet<string> { normalized, phone };

        var results = (await Dal.GetAll(
            filterExprs: [m => m.ConnectionId == connectionId
                          && candidates.Contains(m.To)
                          && m.Status == SmsMessageStatus.Sent],
            orderByDesc: m => m.CreatedAt,
            maxResults: 1)).ToList();

        return results.Count > 0 ? results.First() : null;
    }

    public async Task MarkReplyReceivedAsync(Guid messageId)
    {
        await Dal.Update(messageId, m => m.Status = SmsMessageStatus.ReplyReceived);
    }

    public async Task<UsageStatsDto> GetUsageForUserAsync(Guid userId, int days = 30)
    {
        var connections = await connectionService.GetAllForUserAsync(userId);
        var connectionIds = connections.Select(c => c.Id).ToList();

        if (connectionIds.Count == 0)
        {
            return new UsageStatsDto();
        }

        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var messages = (await Dal.GetAll(
            filterExprs: [m => connectionIds.Contains(m.ConnectionId) && m.CreatedAt >= since]
        )).ToList();

        var byConnection = connections.Select(c =>
        {
            var connMessages = messages.Where(m => m.ConnectionId == c.Id).ToList();
            return new ConnectionUsageDto
            {
                ConnectionId = c.Id,
                ConnectionName = c.Name,
                ProviderType = c.ProviderType,
                Sent = connMessages.Count(m => m.Status == SmsMessageStatus.Sent),
                Failed = connMessages.Count(m => m.Status == SmsMessageStatus.Failed),
                Replies = connMessages.Count(m => m.Status == SmsMessageStatus.ReplyReceived)
            };
        }).Where(c => c.Sent + c.Failed + c.Replies > 0).ToList();

        var daily = messages
            .GroupBy(m => DateOnly.FromDateTime(m.CreatedAt.UtcDateTime))
            .Select(g => new DailyUsageDto
            {
                Date = g.Key,
                Sent = g.Count(m => m.Status == SmsMessageStatus.Sent),
                Failed = g.Count(m => m.Status == SmsMessageStatus.Failed),
                Replies = g.Count(m => m.Status == SmsMessageStatus.ReplyReceived)
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new UsageStatsDto
        {
            TotalSent = messages.Count(m => m.Status == SmsMessageStatus.Sent),
            TotalFailed = messages.Count(m => m.Status == SmsMessageStatus.Failed),
            TotalReplies = messages.Count(m => m.Status == SmsMessageStatus.ReplyReceived),
            ByConnection = byConnection,
            Daily = daily
        };
    }
}
