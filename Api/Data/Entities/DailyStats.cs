using Api.Interfaces;

namespace Api.Data.Entities;

public sealed class DailyStats : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionId { get; set; }
    public DateOnly Date { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Replies { get; set; }
}
