using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Mappings;

public sealed class InboundWebhookReceiptMapping : IEntityTypeConfiguration<InboundWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<InboundWebhookReceipt> builder)
    {
        builder.Property(receipt => receipt.ProviderMessageId).HasMaxLength(200);
        builder
            .HasOne<SmsConnection>()
            .WithMany()
            .HasForeignKey(receipt => receipt.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasIndex(receipt => new { receipt.ConnectionId, receipt.ProviderMessageId })
            .IsUnique()
            .HasDatabaseName("IX_InboundWebhookReceipts_ConnectionId_ProviderMessageId");
    }
}
