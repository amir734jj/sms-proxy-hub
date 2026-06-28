using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shared.Contracts;

namespace Api.Data.Mappings;

public class SmsConnectionMapping : IEntityTypeConfiguration<SmsConnection>
{
    public void Configure(EntityTypeBuilder<SmsConnection> builder)
    {
        // Store SmsProviderType as lowercase string for backwards compatibility
        builder
            .Property(e => e.ProviderType)
            .HasConversion(new ValueConverter<SmsProviderType, string>(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<SmsProviderType>(v, ignoreCase: true)))
            .HasMaxLength(50);
    }
}