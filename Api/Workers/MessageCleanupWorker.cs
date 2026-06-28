using Api.Data.Entities;
using Api.Interfaces;
using EfCoreRepository.Interfaces;
using EfCoreRepository.Extensions;
using Shared;
using Shared.Contracts;

namespace Api.Workers;

public sealed class MessageCleanupWorker(IServiceProvider serviceProvider, ILogger<MessageCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RollupAndCleanAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Message cleanup failed");
            }

            await Task.Delay(Interval, ct);
        }
    }

    private async Task RollupAndCleanAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IEfRepository>();
        var connectionDal = repo.For<SmsConnection>();
        var messageDal = repo.For<SmsMessage>();
        var statsDal = repo.For<DailyStats>();
        var deliveryDal = repo.For<WebhookDelivery>();

        var connections = (await connectionDal.GetAll()).ToList();
        if (connections.Count == 0) return;

        var totalOldMessages = 0;
        var totalOldDeliveries = 0;

        foreach (var connection in connections)
        {
            var retentionDays = connection.MessageRetentionDays > 0 ? connection.MessageRetentionDays : MessageRetention.Days;
            var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

            var oldMessages = (await messageDal.GetAll(
                filterExprs: [m => m.ConnectionId == connection.Id && m.CreatedAt < cutoff]
            )).ToList();

            if (oldMessages.Count > 0)
            {
                // Roll old messages into daily aggregates before deletion.
                var groups = oldMessages
                    .GroupBy(m => DateOnly.FromDateTime(m.CreatedAt.UtcDateTime));

                foreach (var group in groups)
                {
                    var existing = (await statsDal.GetAll(
                        filterExprs: [s => s.ConnectionId == connection.Id && s.Date == group.Key],
                        maxResults: 1
                    )).FirstOrDefault();

                    if (existing is not null)
                    {
                        await statsDal.Update(existing.Id, s =>
                        {
                            s.Sent += group.Count(m => m.Status == SmsMessageStatus.Sent);
                            s.Failed += group.Count(m => m.Status == SmsMessageStatus.Failed);
                            s.Replies += group.Count(m => m.Status == SmsMessageStatus.ReplyReceived);
                        });
                    }
                    else
                    {
                        await statsDal.Save(new DailyStats
                        {
                            ConnectionId = connection.Id,
                            Date = group.Key,
                            Sent = group.Count(m => m.Status == SmsMessageStatus.Sent),
                            Failed = group.Count(m => m.Status == SmsMessageStatus.Failed),
                            Replies = group.Count(m => m.Status == SmsMessageStatus.ReplyReceived)
                        });
                    }
                }

                await messageDal.DeleteMany(oldMessages.Select(m => m.Id).ToArray());
                totalOldMessages += oldMessages.Count;
            }

            var oldDeliveries = (await deliveryDal.GetAll(
                filterExprs: [d => d.ConnectionId == connection.Id && d.CreatedAt < cutoff]
            )).ToList();

            if (oldDeliveries.Count > 0)
            {
                await deliveryDal.DeleteMany(oldDeliveries.Select(d => d.Id).ToArray());
                totalOldDeliveries += oldDeliveries.Count;
            }
        }

        if (totalOldMessages == 0 && totalOldDeliveries == 0) return;

        logger.LogInformation(
            "Cleaned up {MsgCount} messages and {DelCount} webhook deliveries using per-connection retention policies",
            totalOldMessages,
            totalOldDeliveries);
    }
}
