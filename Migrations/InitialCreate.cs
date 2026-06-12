using FluentMigrator;
using Migrations.Extensions;

namespace Migrations;

[Migration(20260611_000)]
public class InitialCreate : Migration
{
    public override void Up()
    {
        // ASP.NET Identity tables

        Create.Table("AspNetRoles")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("Name").AsString(256).Nullable()
            .WithColumn("NormalizedName").AsString(256).Nullable().Unique()
            .WithColumn("ConcurrencyStamp").AsString(int.MaxValue).Nullable();

        Create.Table("AspNetUsers")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("NormalizedUserName").AsString(256).Nullable().Unique()
            .WithColumn("Email").AsString(256).Nullable()
            .WithColumn("NormalizedEmail").AsString(256).Nullable().Indexed()
            .WithColumn("EmailConfirmed").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("PasswordHash").AsString(int.MaxValue).Nullable()
            .WithColumn("SecurityStamp").AsString(int.MaxValue).Nullable()
            .WithColumn("ConcurrencyStamp").AsString(int.MaxValue).Nullable()
            .WithColumn("PhoneNumber").AsString(int.MaxValue).Nullable()
            .WithColumn("PhoneNumberConfirmed").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("TwoFactorEnabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("LockoutEnd").AsDateTimeOffset().Nullable()
            .WithColumn("LockoutEnabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("AccessFailedCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("LastLoginAt").AsDateTimeOffset().Nullable()
            .WithColumn("DisplayName").AsString(256).Nullable();

        Create.Table("AspNetRoleClaims")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RoleId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetRoleClaims_AspNetRoles", "AspNetRoles", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ClaimType").AsString(int.MaxValue).Nullable()
            .WithColumn("ClaimValue").AsString(int.MaxValue).Nullable();

        Create.Table("AspNetUserClaims")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetUserClaims_AspNetUsers", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ClaimType").AsString(int.MaxValue).Nullable()
            .WithColumn("ClaimValue").AsString(int.MaxValue).Nullable();

        Create.Table("AspNetUserLogins")
            .WithColumn("LoginProvider").AsString(450).NotNullable()
            .WithColumn("ProviderKey").AsString(450).NotNullable()
            .WithColumn("ProviderDisplayName").AsString(int.MaxValue).Nullable()
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetUserLogins_AspNetUsers", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade);

        Create.PrimaryKey("PK_AspNetUserLogins").OnTable("AspNetUserLogins").Columns("LoginProvider", "ProviderKey");

        Create.Table("AspNetUserRoles")
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetUserRoles_AspNetUsers", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("RoleId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetUserRoles_AspNetRoles", "AspNetRoles", "Id").OnDelete(System.Data.Rule.Cascade);

        Create.PrimaryKey("PK_AspNetUserRoles").OnTable("AspNetUserRoles").Columns("UserId", "RoleId");

        Create.Table("AspNetUserTokens")
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetUserTokens_AspNetUsers", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("LoginProvider").AsString(450).NotNullable()
            .WithColumn("Name").AsString(450).NotNullable()
            .WithColumn("Value").AsString(int.MaxValue).Nullable();

        Create.PrimaryKey("PK_AspNetUserTokens").OnTable("AspNetUserTokens").Columns("UserId", "LoginProvider", "Name");

        // SmsConnections -- single table for all providers, config stored as JSON
        Create.Table("SmsConnections")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_SmsConnections_AspNetUsers", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("ProviderType").AsString(50).NotNullable()
            .WithColumn("ConfigJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_SmsConnections_UserId").OnTable("SmsConnections").OnColumn("UserId");

        // SmsMessages -- tracks every SMS for webhook correlation
        Create.Table("SmsMessages")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("ConnectionId").AsGuid().NotNullable()
                .ForeignKey("FK_SmsMessages_SmsConnections", "SmsConnections", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("To").AsString(30).NotNullable().Indexed()
            .WithColumn("Message").AsString(1000).NotNullable()
            .WithColumn("ProviderMessageId").AsString(200).Nullable()
            .WithColumn("Payload").AsString(int.MaxValue).Nullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_SmsMessages_ConnectionId").OnTable("SmsMessages").OnColumn("ConnectionId");

        // WebhookSubscriptions
        Create.Table("WebhookSubscriptions")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("ConnectionId").AsGuid().NotNullable()
                .ForeignKey("FK_WebhookSubscriptions_SmsConnections", "SmsConnections", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Url").AsString(500).NotNullable()
            .WithColumn("Secret").AsString(200).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_WebhookSubscriptions_ConnectionId").OnTable("WebhookSubscriptions").OnColumn("ConnectionId");

        // ApiTokens -- programmatic access for consumers like xldent
        Create.Table("ApiTokens")
            .WithColumn("Id").AsGuid().PrimaryKey().WithDefaultValue(MigrationExtensions.GenRandomUuid)
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_ApiTokens_AspNetUsers", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Token").AsString(100).NotNullable().Unique()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_ApiTokens_UserId").OnTable("ApiTokens").OnColumn("UserId");
    }

    public override void Down()
    {
        Delete.Table("ApiTokens");
        Delete.Table("WebhookSubscriptions");
        Delete.Table("SmsMessages");
        Delete.Table("SmsConnections");
        Delete.Table("AspNetUserTokens");
        Delete.Table("AspNetUserRoles");
        Delete.Table("AspNetUserLogins");
        Delete.Table("AspNetUserClaims");
        Delete.Table("AspNetRoleClaims");
        Delete.Table("AspNetUsers");
        Delete.Table("AspNetRoles");
    }
}
