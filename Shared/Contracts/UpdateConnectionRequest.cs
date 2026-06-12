namespace Shared.Contracts;

public record UpdateConnectionRequest(string Name, SmsConnectionConfig Config, bool IsActive);
