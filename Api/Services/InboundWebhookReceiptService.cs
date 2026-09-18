using Api.Data.Entities;
using Api.Interfaces;
using EfCoreRepository.Extensions;
using EfCoreRepository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

public sealed class InboundWebhookReceiptService(IEfRepository repository)
    : IInboundWebhookReceiptService
{
    private const string UniqueConstraint =
        "IX_InboundWebhookReceipts_ConnectionId_ProviderMessageId";

    private IBasicCrud<InboundWebhookReceipt> Dal =>
        repository.For<InboundWebhookReceipt>();

    public async Task<bool> TryClaimAsync(
        Guid connectionId,
        string providerMessageId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await Dal.Save(new InboundWebhookReceipt
            {
                ConnectionId = connectionId,
                ProviderMessageId = providerMessageId
            });
            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: UniqueConstraint
            })
        {
            return false;
        }
    }
}
