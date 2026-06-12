using Refit;

namespace Shared.Contracts.Interfaces;

public interface IProfileApi
{
    [Put("/api/profile")]
    [Headers("Authorization: Bearer")]
    Task UpdateAsync([Body] UpdateProfileRequest request);

    [Post("/api/profile/change-password")]
    [Headers("Authorization: Bearer")]
    Task ChangePasswordAsync([Body] ChangePasswordRequest request);
}
