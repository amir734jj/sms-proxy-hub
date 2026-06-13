using Refit;

namespace Shared.Contracts.Interfaces;

public interface IConnectionsApi
{
    [Get("/api/connections")]
    [Headers("Authorization: Bearer")]
    Task<List<SmsConnectionDto>> GetAllAsync();

    [Post("/api/connections")]
    [Headers("Authorization: Bearer")]
    Task<SmsConnectionDto> CreateAsync([Body] CreateConnectionRequest request);

    [Put("/api/connections/{id}")]
    [Headers("Authorization: Bearer")]
    Task UpdateAsync(Guid id, [Body] UpdateConnectionRequest request);

    [Delete("/api/connections/{id}")]
    [Headers("Authorization: Bearer")]
    Task DeleteAsync(Guid id);

    [Post("/api/connections/smsgate-devices")]
    [Headers("Authorization: Bearer")]
    Task<List<SmsGateDeviceDto>> GetSmsGateDevicesAsync([Body] SmsGateConnectionConfig config);

    [Post("/api/connections/reorder")]
    [Headers("Authorization: Bearer")]
    Task ReorderAsync([Body] List<Guid> orderedIds);
}
