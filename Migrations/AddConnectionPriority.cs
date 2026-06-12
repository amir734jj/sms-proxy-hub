using FluentMigrator;

namespace Migrations;

[Migration(20260611_001)]
public class AddConnectionPriority : Migration
{
    public override void Up()
    {
        Alter.Table("SmsConnections")
            .AddColumn("Priority").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("Priority").FromTable("SmsConnections");
    }
}
