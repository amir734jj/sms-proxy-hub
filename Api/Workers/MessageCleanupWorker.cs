using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace Api.Workers;

public sealed class MessageCleanupWorker(IServiceProvider serviceProvider, ILogger<MessageCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private const int RetentionDays = 7;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // wait for app to fully start
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
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);

        // get old messages grouped by connection + date
        var oldMessages = await db.SmsMessages
            .Where(m => m.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (oldMessages.Count == 0) return;

        var groups = oldMessages
            .GroupBy(m => (m.ConnectionId, Date: DateOnly.FromDateTime(m.CreatedAt.UtcDateTime)));

        foreach (var group in groups)
        {
            var existing = await db.Set<DailyStats>()
                .FirstOrDefaultAsync(s => s.ConnectionId == group.Key.ConnectionId && s.Date == group.Key.Date, ct);

            if (existing is not null)
            {
                existing.Sent += group.Count(m => m.Status == SmsMessageStatus.Sent);
                existing.Failed += group.Count(m => m.Status == SmsMessageStatus.Failed);
                existing.Replies += group.Count(m => m.Status == SmsMessageStatus.ReplyReceived);
            }
            else
            {
                db.Set<DailyStats>().Add(new DailyStats
                {
                    ConnectionId = group.Key.ConnectionId,
                    Date = group.Key.Date,
                    Sent = group.Count(m => m.Status == SmsMessageStatus.Sent),
                    Failed = group.Count(m => m.Status == SmsMessageStatus.Failed),
                    Replies = group.Count(m => m.Status == SmsMessageStatus.ReplyReceived)
                });
            }
        }

        db.SmsMessages.RemoveRange(oldMessages);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Cleaned up {Count} messages older than {Days} days", oldMessages.Count, RetentionDays);
    }
}
