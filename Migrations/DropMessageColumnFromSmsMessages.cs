using FluentMigrator;

namespace Migrations;

[Migration(20260628_003)]
public class DropMessageColumnFromSmsMessages : Migration
{
    public override void Up()
    {
        // Message text is never persisted, so the column has no purpose.
        Delete.Column("Message").FromTable("SmsMessages");
    }

    public override void Down()
    {
        Alter.Table("SmsMessages")
            .AddColumn("Message").AsString(1000).NotNullable().WithDefaultValue(string.Empty);
    }
}
