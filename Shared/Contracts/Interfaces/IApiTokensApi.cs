using Refit;

namespace Shared.Contracts.Interfaces;

public interface IApiTokensApi
{
    [Get("/api/tokens")]
    [Headers("Authorization: Bearer")]
    Task<List<ApiTokenDto>> GetAllAsync();

    [Post("/api/tokens")]
    [Headers("Authorization: Bearer")]
    Task<ApiTokenDto> CreateAsync([Body] CreateApiTokenRequest request);

    [Delete("/api/tokens/{id}")]
    [Headers("Authorization: Bearer")]
    Task DeleteAsync(Guid id);
}
