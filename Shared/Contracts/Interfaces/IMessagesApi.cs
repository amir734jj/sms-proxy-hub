using Refit;

namespace Shared.Contracts.Interfaces;

public interface IMessagesApi
{
    [Post("/api/messages/send")]
    [Headers("Authorization: Bearer")]
    Task<SendSmsResponse> SendAsync([Body] SendSmsRequest request);

    [Get("/api/messages")]
    [Headers("Authorization: Bearer")]
    Task<List<SmsMessageDto>> GetAllAsync();

    [Get("/api/messages/connection/{connectionId}")]
    [Headers("Authorization: Bearer")]
    Task<List<SmsMessageDto>> GetByConnectionAsync(Guid connectionId);
}
