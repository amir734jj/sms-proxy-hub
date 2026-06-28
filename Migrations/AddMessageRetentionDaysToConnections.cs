using FluentMigrator;

namespace Migrations;

[Migration(20260628_001)]
public class AddMessageRetentionDaysToConnections : Migration
{
    public override void Up()
    {
        Alter.Table("SmsConnections")
            .AddColumn("MessageRetentionDays").AsInt32().NotNullable().WithDefaultValue(7);
    }

    public override void Down()
    {
        Delete.Column("MessageRetentionDays").FromTable("SmsConnections");
    }
}
