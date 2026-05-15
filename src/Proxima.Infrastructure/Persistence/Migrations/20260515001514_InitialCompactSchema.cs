using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proxima.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCompactSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "public");

        migrationBuilder.CreateTable(
            name: "quote_cache",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                price = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                source = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_quote_cache", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tags",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tags", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                login = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                failed_unlock_attempts = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "audit_log",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                action = table.Column<string>(type: "text", nullable: false),
                timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                metadata_json = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_log", x => x.id);
                table.ForeignKey(
                    name: "FK_audit_log_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "notifications",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notifications", x => x.id);
                table.ForeignKey(
                    name: "FK_notifications_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "portfolios",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                base_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                client_label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_portfolios", x => x.id);
                table.ForeignKey(
                    name: "FK_portfolios_users_owner_user_id",
                    column: x => x.owner_user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "user_settings",
            schema: "public",
            columns: table => new
            {
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                login = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                preferred_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ui_scale = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                quote_provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                quote_refresh_minutes = table.Column<int>(type: "integer", nullable: false),
                twelve_data_api_key_protected = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                currency_provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_settings", x => x.owner_user_id);
                table.ForeignKey(
                    name: "FK_user_settings_users_owner_user_id",
                    column: x => x.owner_user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "assets",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                exchange = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                isin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                encrypted_notes = table.Column<string>(type: "text", nullable: true),
                quantity = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                average_buy_price = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                current_price = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                is_archived = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assets", x => x.id);
                table.ForeignKey(
                    name: "FK_assets_portfolios_portfolio_id",
                    column: x => x.portfolio_id,
                    principalSchema: "public",
                    principalTable: "portfolios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "goals",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "text", nullable: false),
                target_amount = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                monthly_contribution = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                expected_annual_return_percent = table.Column<decimal>(type: "numeric", nullable: true),
                target_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_goals", x => x.id);
                table.ForeignKey(
                    name: "FK_goals_portfolios_portfolio_id",
                    column: x => x.portfolio_id,
                    principalSchema: "public",
                    principalTable: "portfolios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "asset_prices",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                price = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asset_prices", x => x.id);
                table.ForeignKey(
                    name: "FK_asset_prices_assets_asset_id",
                    column: x => x.asset_id,
                    principalSchema: "public",
                    principalTable: "assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asset_tags",
            schema: "public",
            columns: table => new
            {
                asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asset_tags", x => new { x.asset_id, x.tag_id });
                table.ForeignKey(
                    name: "FK_asset_tags_assets_asset_id",
                    column: x => x.asset_id,
                    principalSchema: "public",
                    principalTable: "assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_asset_tags_tags_tag_id",
                    column: x => x.tag_id,
                    principalSchema: "public",
                    principalTable: "tags",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "transactions",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                type = table.Column<string>(type: "text", nullable: false),
                trade_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                price = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                gross_amount = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                fee_amount = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                tax_amount = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: false),
                currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                broker = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                encrypted_notes = table.Column<string>(type: "text", nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transactions", x => x.id);
                table.ForeignKey(
                    name: "FK_transactions_assets_asset_id",
                    column: x => x.asset_id,
                    principalSchema: "public",
                    principalTable: "assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_transactions_portfolios_portfolio_id",
                    column: x => x.portfolio_id,
                    principalSchema: "public",
                    principalTable: "portfolios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_asset_prices_asset_id_timestamp",
            schema: "public",
            table: "asset_prices",
            columns: new[] { "asset_id", "timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_asset_tags_tag_id",
            schema: "public",
            table: "asset_tags",
            column: "tag_id");

        migrationBuilder.CreateIndex(
            name: "IX_assets_portfolio_id_is_archived",
            schema: "public",
            table: "assets",
            columns: new[] { "portfolio_id", "is_archived" });

        migrationBuilder.CreateIndex(
            name: "IX_audit_log_timestamp",
            schema: "public",
            table: "audit_log",
            column: "timestamp");

        migrationBuilder.CreateIndex(
            name: "IX_audit_log_user_id",
            schema: "public",
            table: "audit_log",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_goals_portfolio_id",
            schema: "public",
            table: "goals",
            column: "portfolio_id");

        migrationBuilder.CreateIndex(
            name: "IX_notifications_user_id_deleted_at_utc_created_at_utc",
            schema: "public",
            table: "notifications",
            columns: new[] { "user_id", "deleted_at_utc", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_portfolios_owner_user_id_is_archived",
            schema: "public",
            table: "portfolios",
            columns: new[] { "owner_user_id", "is_archived" });

        migrationBuilder.CreateIndex(
            name: "IX_quote_cache_asset_id",
            schema: "public",
            table: "quote_cache",
            column: "asset_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tags_name",
            schema: "public",
            table: "tags",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_transactions_asset_id",
            schema: "public",
            table: "transactions",
            column: "asset_id");

        migrationBuilder.CreateIndex(
            name: "IX_transactions_portfolio_id_trade_date",
            schema: "public",
            table: "transactions",
            columns: new[] { "portfolio_id", "trade_date" });

        migrationBuilder.CreateIndex(
            name: "IX_users_login",
            schema: "public",
            table: "users",
            column: "login",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "asset_prices",
            schema: "public");

        migrationBuilder.DropTable(
            name: "asset_tags",
            schema: "public");

        migrationBuilder.DropTable(
            name: "audit_log",
            schema: "public");

        migrationBuilder.DropTable(
            name: "goals",
            schema: "public");

        migrationBuilder.DropTable(
            name: "notifications",
            schema: "public");

        migrationBuilder.DropTable(
            name: "quote_cache",
            schema: "public");

        migrationBuilder.DropTable(
            name: "transactions",
            schema: "public");

        migrationBuilder.DropTable(
            name: "user_settings",
            schema: "public");

        migrationBuilder.DropTable(
            name: "tags",
            schema: "public");

        migrationBuilder.DropTable(
            name: "assets",
            schema: "public");

        migrationBuilder.DropTable(
            name: "portfolios",
            schema: "public");

        migrationBuilder.DropTable(
            name: "users",
            schema: "public");
    }
}
