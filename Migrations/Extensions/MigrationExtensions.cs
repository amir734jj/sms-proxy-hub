using FluentMigrator;

namespace Migrations.Extensions;

public static class MigrationExtensions
{
    public static RawSql GenRandomUuid => RawSql.Insert("gen_random_uuid()");
}
