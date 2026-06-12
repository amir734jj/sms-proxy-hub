using Api.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shared.Contracts;

namespace Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<User, Role, Guid>(options)
{
    public DbSet<SmsConnection> SmsConnections => Set<SmsConnection>();
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Store SmsProviderType as lowercase string for backwards compatibility
        builder.Entity<SmsConnection>()
            .Property(e => e.ProviderType)
            .HasConversion(new ValueConverter<SmsProviderType, string>(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<SmsProviderType>(v, ignoreCase: true)))
            .HasMaxLength(50);
    }
}
