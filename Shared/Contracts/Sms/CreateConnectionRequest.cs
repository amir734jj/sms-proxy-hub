using Shared;

namespace Shared.Contracts;

public record CreateConnectionRequest(string Name, SmsConnectionConfig Config, int MessageRetentionDays = MessageRetention.Days);
