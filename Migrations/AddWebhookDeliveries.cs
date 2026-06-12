using FluentMigrator;
using Migrations.Extensions;

namespace Migrations;

[Migration(20260612_001)]
public class AddWebhookDeliveries : Migration
{
    public override void Up()
    {
        Create.Table("WebhookDeliveries")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("WebhookSubscriptionId").AsGuid().NotNullable()
                .ForeignKey("FK_WebhookDeliveries_WebhookSubscriptions", "WebhookSubscriptions", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ConnectionId").AsGuid().NotNullable()
            .WithColumn("Event").AsString(50).NotNullable()
            .WithColumn("Url").AsString(500).NotNullable()
            .WithColumn("RequestBody").AsString(int.MaxValue).NotNullable()
            .WithColumn("HttpStatus").AsInt32().Nullable()
            .WithColumn("Success").AsBoolean().NotNullable()
            .WithColumn("Error").AsString(1000).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_WebhookDeliveries_ConnectionId").OnTable("WebhookDeliveries").OnColumn("ConnectionId");
        Create.Index("IX_WebhookDeliveries_WebhookSubscriptionId").OnTable("WebhookDeliveries").OnColumn("WebhookSubscriptionId");
    }

    public override void Down()
    {
        Delete.Table("WebhookDeliveries");
    }
}
