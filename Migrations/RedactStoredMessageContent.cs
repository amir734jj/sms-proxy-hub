using FluentMigrator;

namespace Migrations;

[Migration(20260628_002)]
public class RedactStoredMessageContent : Migration
{
    public override void Up()
    {
        // Privacy: message text must never be persisted at rest. Wipe content from existing rows.
        Execute.Sql("UPDATE \"SmsMessages\" SET \"Message\" = ''");
        Execute.Sql("UPDATE \"WebhookDeliveries\" SET \"RequestBody\" = ''");
    }

    public override void Down()
    {
        // Irreversible: the original message content was intentionally discarded.
    }
}
