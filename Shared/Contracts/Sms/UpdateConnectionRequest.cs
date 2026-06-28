using Shared;

namespace Shared.Contracts;

public record UpdateConnectionRequest(string Name, SmsConnectionConfig Config, bool IsActive, int Priority, int MessageRetentionDays = MessageRetention.Days);
