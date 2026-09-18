namespace Api.Interfaces;

public interface IInboundWebhookReceiptService
{
    Task<bool> TryClaimAsync(
        Guid connectionId,
        string providerMessageId,
        CancellationToken cancellationToken = default);
}
