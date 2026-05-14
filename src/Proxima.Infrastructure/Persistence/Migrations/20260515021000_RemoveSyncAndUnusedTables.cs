using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Proxima.Infrastructure.Persistence;

#nullable disable

namespace Proxima.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ProximaDbContext))]
[Migration("20260515021000_RemoveSyncAndUnusedTables")]
public partial class RemoveSyncAndUnusedTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            drop table if exists sync_snapshots cascade;
            drop table if exists import_rows cascade;
            drop table if exists import_sessions cascade;
            drop table if exists tax_reports cascade;
            drop table if exists tax_profiles cascade;

            alter table if exists user_settings drop column if exists sync_enabled;
            alter table if exists user_settings drop column if exists last_snapshot_at;
            alter table if exists user_settings drop column if exists finnhub_api_key_protected;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: these were unused legacy/sync tables and should not be recreated.
    }
}
