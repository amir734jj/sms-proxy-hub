namespace Shared.Contracts;

public record CreateConnectionRequest(string Name, SmsConnectionConfig Config, int Priority = 0);
