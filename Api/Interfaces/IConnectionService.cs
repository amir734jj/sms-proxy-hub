using Shared.Contracts;

namespace Api.Interfaces;

public interface IConnectionService
{
    Task<List<SmsConnectionDto>> GetAllForUserAsync(Guid userId);
    Task<SmsConnectionDto> CreateAsync(Guid userId, CreateConnectionRequest request);
    Task<bool> UpdateAsync(Guid userId, Guid id, UpdateConnectionRequest request);
    Task<bool> DeleteAsync(Guid userId, Guid id);
    Task<Data.Entities.SmsConnection?> GetByIdAsync(Guid id);
    Task<bool> UserOwnsConnectionAsync(Guid userId, Guid connectionId);
    Task<List<Data.Entities.SmsConnection>> GetActiveForUserInPriorityOrderAsync(Guid userId);
    Task<bool> ReorderAsync(Guid userId, List<Guid> orderedIds);
}
