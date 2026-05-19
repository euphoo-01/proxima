using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proxima.Infrastructure.Persistence.Migrations;

public partial class UserProfileMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "location",
            schema: "public",
            table: "users",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "Минск, Беларусь");

        migrationBuilder.AddColumn<string>(
            name: "legal_profile",
            schema: "public",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "PhysicalPerson");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "location",
            schema: "public",
            table: "users");

        migrationBuilder.DropColumn(
            name: "legal_profile",
            schema: "public",
            table: "users");
    }
}
