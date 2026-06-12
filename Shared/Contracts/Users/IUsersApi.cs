using Refit;

namespace Shared.Contracts.Interfaces;

public interface IUsersApi
{
    [Get("/api/users")]
    [Headers("Authorization: Bearer")]
    Task<List<UserDto>> GetAllAsync();

    [Post("/api/users/{id}/activate")]
    [Headers("Authorization: Bearer")]
    Task ActivateAsync(Guid id);

    [Post("/api/users/{id}/deactivate")]
    [Headers("Authorization: Bearer")]
    Task DeactivateAsync(Guid id);

    [Delete("/api/users/{id}")]
    [Headers("Authorization: Bearer")]
    Task DeleteAsync(Guid id);
}
