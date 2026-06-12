using Api.Data.Entities;
using Api.Interfaces;
using EfCoreRepository.Interfaces;
using EfCoreRepository.Extensions;
using Shared.Contracts;

namespace Api.Services;

public sealed class ApiTokenService(IEfRepository repository) : IApiTokenService
{
    private IBasicCrud<ApiToken> Dal => repository.For<ApiToken>();

    public async Task<List<ApiTokenDto>> GetAllForUserAsync(Guid userId)
    {
        return (await Dal.GetAll(
            filterExprs: [t => t.UserId == userId],
            orderByDesc: t => t.CreatedAt,
            project: t => new ApiTokenDto(t.Id, t.Token, t.Name, t.IsActive, t.CreatedAt)
        )).ToList();
    }

    public async Task<ApiTokenDto> CreateAsync(Guid userId, string name)
    {
        var entity = await Dal.Save(new ApiToken
        {
            UserId = userId,
            Name = name.Trim()
        });

        return new ApiTokenDto(entity.Id, entity.Token, entity.Name, entity.IsActive, entity.CreatedAt);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id)
    {
        if (!await Dal.Any([t => t.Id == id && t.UserId == userId])) return false;
        await Dal.Delete(id);
        return true;
    }

    public async Task<(Guid UserId, Guid TokenId)?> ValidateTokenAsync(string token)
    {
        var items = (await Dal.GetAll(
            filterExprs: [t => t.Token == token && t.IsActive],
            maxResults: 1)).ToList();

        return items.Count > 0 ? (items.First().UserId, items.First().Id) : null;
    }
}
