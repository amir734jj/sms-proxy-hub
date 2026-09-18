using FluentMigrator;
using Migrations.Extensions;

namespace Migrations;

[Migration(20260918_001)]
public sealed class AddInboundWebhookReceipts : Migration
{
    public override void Up()
    {
        Create.Table("InboundWebhookReceipts")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("ConnectionId").AsGuid().NotNullable()
                .ForeignKey("FK_InboundWebhookReceipts_SmsConnections", "SmsConnections", "Id")
                .OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ProviderMessageId").AsString(200).NotNullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
                .WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_InboundWebhookReceipts_ConnectionId_ProviderMessageId")
            .OnTable("InboundWebhookReceipts")
            .OnColumn("ConnectionId").Ascending()
            .OnColumn("ProviderMessageId").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("InboundWebhookReceipts");
    }
}
