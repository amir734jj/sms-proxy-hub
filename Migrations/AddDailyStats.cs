using FluentMigrator;
using Migrations.Extensions;

namespace Migrations;

[Migration(20260612_002)]
public class AddDailyStats : Migration
{
    public override void Up()
    {
        Create.Table("DailyStats")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("ConnectionId").AsGuid().NotNullable()
                .ForeignKey("FK_DailyStats_SmsConnections", "SmsConnections", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Date").AsDate().NotNullable()
            .WithColumn("Sent").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Failed").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Replies").AsInt32().NotNullable().WithDefaultValue(0);

        Create.Index("IX_DailyStats_ConnectionId_Date")
            .OnTable("DailyStats")
            .OnColumn("ConnectionId").Ascending()
            .OnColumn("Date").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("DailyStats");
    }
}
