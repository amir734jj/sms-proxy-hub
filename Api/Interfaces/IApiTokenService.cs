using Shared.Contracts;

namespace Api.Interfaces;

public interface IApiTokenService
{
    Task<List<ApiTokenDto>> GetAllForUserAsync(Guid userId);
    Task<ApiTokenDto> CreateAsync(Guid userId, string name);
    Task<bool> DeleteAsync(Guid userId, Guid id);
    Task<(Guid UserId, Guid TokenId)?> ValidateTokenAsync(string token);
}
