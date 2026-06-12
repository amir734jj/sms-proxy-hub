namespace Shared.Contracts;

public class UsageStatsDto
{
    public int TotalSent { get; init; }
    public int TotalFailed { get; init; }
    public int TotalReplies { get; init; }
    public List<ConnectionUsageDto> ByConnection { get; init; } = [];
    public List<DailyUsageDto> Daily { get; init; } = [];
}

public class ConnectionUsageDto
{
    public Guid ConnectionId { get; init; }
    public string ConnectionName { get; init; } = string.Empty;
    public SmsProviderType ProviderType { get; init; }
    public int Sent { get; init; }
    public int Failed { get; init; }
    public int Replies { get; init; }
}

public class DailyUsageDto
{
    public DateOnly Date { get; init; }
    public int Sent { get; init; }
    public int Failed { get; init; }
    public int Replies { get; init; }
}
