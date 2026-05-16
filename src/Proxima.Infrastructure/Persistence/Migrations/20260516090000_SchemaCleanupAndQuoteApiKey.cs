using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proxima.Infrastructure.Persistence.Migrations;

public partial class SchemaCleanupAndQuoteApiKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "encrypted_notes",
            schema: "public",
            table: "assets");

        migrationBuilder.DropColumn(
            name: "base_currency",
            schema: "public",
            table: "portfolios");

        migrationBuilder.DropColumn(
            name: "preferred_currency",
            schema: "public",
            table: "user_settings");

        migrationBuilder.DropColumn(
            name: "language",
            schema: "public",
            table: "user_settings");

        migrationBuilder.DropColumn(
            name: "ui_scale",
            schema: "public",
            table: "user_settings");

        migrationBuilder.DropColumn(
            name: "quote_refresh_minutes",
            schema: "public",
            table: "user_settings");

        migrationBuilder.RenameColumn(
            name: "twelve_data_api_key_protected",
            schema: "public",
            table: "user_settings",
            newName: "quote_api_key");

        migrationBuilder.RenameColumn(
            name: "encrypted_notes",
            schema: "public",
            table: "transactions",
            newName: "notes");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "quote_api_key",
            schema: "public",
            table: "user_settings",
            newName: "twelve_data_api_key_protected");

        migrationBuilder.RenameColumn(
            name: "notes",
            schema: "public",
            table: "transactions",
            newName: "encrypted_notes");

        migrationBuilder.AddColumn<string>(
            name: "encrypted_notes",
            schema: "public",
            table: "assets",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "base_currency",
            schema: "public",
            table: "portfolios",
            type: "character varying(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "USD");

        migrationBuilder.AddColumn<string>(
            name: "preferred_currency",
            schema: "public",
            table: "user_settings",
            type: "character varying(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "USD");

        migrationBuilder.AddColumn<string>(
            name: "language",
            schema: "public",
            table: "user_settings",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "RU");

        migrationBuilder.AddColumn<decimal>(
            name: "ui_scale",
            schema: "public",
            table: "user_settings",
            type: "numeric(10,4)",
            precision: 10,
            scale: 4,
            nullable: false,
            defaultValue: 1m);

        migrationBuilder.AddColumn<int>(
            name: "quote_refresh_minutes",
            schema: "public",
            table: "user_settings",
            type: "integer",
            nullable: false,
            defaultValue: 15);
    }
}
