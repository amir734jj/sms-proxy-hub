using Api.Data.Entities;
using Api.Interfaces;
using EfCoreRepository.Interfaces;
using EfCoreRepository.Extensions;
using Newtonsoft.Json;
using Shared.Contracts;

namespace Api.Services;

public sealed class ConnectionService(IEfRepository repository) : IConnectionService
{
    private IBasicCrud<SmsConnection> Dal => repository.For<SmsConnection>();

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None
    };

    public async Task<List<SmsConnectionDto>> GetAllForUserAsync(Guid userId)
    {
        return (await Dal.GetAll(
            filterExprs: [c => c.UserId == userId],
            orderByDesc: c => c.CreatedAt,
            project: c => new SmsConnectionDto(c.Id, c.Name, c.ProviderType, c.IsActive, c.CreatedAt)
        )).ToList();
    }

    public async Task<SmsConnectionDto> CreateAsync(Guid userId, CreateConnectionRequest request)
    {
        var entity = await Dal.Save(new SmsConnection
        {
            UserId = userId,
            Name = request.Name.Trim(),
            ProviderType = request.Config.Type,
            ConfigJson = JsonConvert.SerializeObject(request.Config, JsonSettings)
        });

        return new SmsConnectionDto(entity.Id, entity.Name, entity.ProviderType, entity.IsActive, entity.CreatedAt);
    }

    public async Task<bool> UpdateAsync(Guid userId, Guid id, UpdateConnectionRequest request)
    {
        if (!await Dal.Any([c => c.Id == id && c.UserId == userId])) return false;

        await Dal.Update(id, c =>
        {
            c.Name = request.Name.Trim();
            c.ProviderType = request.Config.Type;
            c.ConfigJson = JsonConvert.SerializeObject(request.Config, JsonSettings);
            c.IsActive = request.IsActive;
        });

        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id)
    {
        if (!await Dal.Any([c => c.Id == id && c.UserId == userId])) return false;
        await Dal.Delete(id);
        return true;
    }

    public async Task<SmsConnection?> GetByIdAsync(Guid id)
    {
        var results = (await Dal.GetAll(
            filterExprs: [c => c.Id == id],
            maxResults: 1)).ToList();
        return results.Count > 0 ? results.First() : null;
    }

    public Task<bool> UserOwnsConnectionAsync(Guid userId, Guid connectionId)
    {
        return Dal.Any([c => c.Id == connectionId && c.UserId == userId]);
    }
}
