using Api.Data.Entities;
using Api.Interfaces;
using EfCoreRepository.Interfaces;
using EfCoreRepository.Extensions;
using Shared.Contracts;

namespace Api.Workers;

public sealed class MessageCleanupWorker(IServiceProvider serviceProvider, ILogger<MessageCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private const int RetentionDays = 7;

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
        var messageDal = repo.For<SmsMessage>();
        var statsDal = repo.For<DailyStats>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);

        var oldMessages = (await messageDal.GetAll(
            filterExprs: [m => m.CreatedAt < cutoff]
        )).ToList();

        if (oldMessages.Count == 0) return;

        // roll up into daily stats
        var groups = oldMessages
            .GroupBy(m => (m.ConnectionId, Date: DateOnly.FromDateTime(m.CreatedAt.UtcDateTime)));

        foreach (var group in groups)
        {
            var existing = (await statsDal.GetAll(
                filterExprs: [s => s.ConnectionId == group.Key.ConnectionId && s.Date == group.Key.Date],
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
                    ConnectionId = group.Key.ConnectionId,
                    Date = group.Key.Date,
                    Sent = group.Count(m => m.Status == SmsMessageStatus.Sent),
                    Failed = group.Count(m => m.Status == SmsMessageStatus.Failed),
                    Replies = group.Count(m => m.Status == SmsMessageStatus.ReplyReceived)
                });
            }
        }

        // delete old messages in bulk
        await messageDal.DeleteMany(oldMessages.Select(m => m.Id).ToArray());

        logger.LogInformation("Cleaned up {Count} messages older than {Days} days", oldMessages.Count, RetentionDays);
    }
}
