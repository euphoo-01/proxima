using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Proxima.Infrastructure.Persistence;

#nullable disable

namespace Proxima.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ProximaDbContext))]
[Migration("20260515020000_InitialPostgreSqlSchema")]
public partial class InitialPostgreSqlSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            create table if not exists users (
              id uuid primary key,
              display_name varchar(128) not null,
              login varchar(128) not null,
              role varchar(64) not null,
              password_algorithm varchar(64) not null default '',
              password_salt bytea not null default decode('', 'hex'),
              password_hash bytea not null default decode('', 'hex'),
              password_iterations int not null default 0,
              password_version int not null default 0,
              failed_unlock_attempts int not null default 0,
              created_at timestamptz not null,
              updated_at timestamptz not null
            );
            create unique index if not exists ix_users_login on users(login);

            create table if not exists portfolios (
              id uuid primary key,
              owner_user_id uuid not null references users(id) on delete restrict,
              name text not null,
              base_currency varchar(8) not null,
              description varchar(1024) null,
              client_label varchar(256) null,
              is_archived boolean not null default false,
              created_at timestamptz not null,
              updated_at timestamptz not null
            );
            create index if not exists ix_portfolios_owner_user_id_is_archived on portfolios(owner_user_id, is_archived);

            create table if not exists assets (
              id uuid primary key,
              portfolio_id uuid not null references portfolios(id) on delete restrict,
              ticker varchar(32) not null,
              name text not null,
              type text not null,
              currency varchar(8) not null,
              exchange varchar(128) null,
              isin varchar(32) null,
              encrypted_notes text null,
              quantity numeric(20,8) not null,
              average_buy_price numeric(20,8) not null,
              current_price numeric(20,8) not null,
              is_archived boolean not null default false,
              created_at timestamptz not null,
              updated_at timestamptz not null
            );
            create index if not exists ix_assets_portfolio_id_is_archived on assets(portfolio_id, is_archived);

            create table if not exists tags (
              id uuid primary key,
              name varchar(64) not null
            );
            create unique index if not exists ix_tags_name on tags(name);

            create table if not exists asset_tags (
              asset_id uuid not null references assets(id) on delete cascade,
              tag_id uuid not null references tags(id) on delete restrict,
              primary key(asset_id, tag_id)
            );

            create table if not exists transactions (
              id uuid primary key,
              portfolio_id uuid not null references portfolios(id) on delete restrict,
              asset_id uuid null references assets(id) on delete restrict,
              type text not null,
              trade_date timestamptz not null,
              quantity numeric(20,8) not null,
              price numeric(20,8) not null,
              gross_amount numeric(20,8) not null,
              fee_amount numeric(20,8) not null,
              tax_amount numeric(20,8) not null,
              currency varchar(8) not null,
              broker varchar(128) null,
              external_id varchar(128) null,
              encrypted_notes text null,
              is_archived boolean not null default false,
              created_at timestamptz not null,
              updated_at timestamptz not null
            );
            create index if not exists ix_transactions_portfolio_id_trade_date on transactions(portfolio_id, trade_date desc);

            create table if not exists asset_prices (
              id uuid primary key,
              asset_id uuid not null references assets(id) on delete cascade,
              price numeric(20,8) not null,
              currency varchar(8) not null,
              timestamp timestamptz not null
            );
            create index if not exists ix_asset_prices_asset_id_timestamp on asset_prices(asset_id, timestamp desc);

            create table if not exists goals (
              id uuid primary key,
              portfolio_id uuid not null references portfolios(id) on delete restrict,
              title text not null,
              target_amount numeric(20,8) not null,
              currency varchar(8) not null default 'USD',
              monthly_contribution numeric(20,8) not null,
              expected_annual_return_percent numeric null,
              target_date timestamptz null,
              is_archived boolean not null default false,
              created_at timestamptz not null,
              updated_at timestamptz not null
            );

            create table if not exists quote_cache (
              id uuid primary key,
              asset_id uuid not null unique,
              ticker varchar(32) not null default '',
              price numeric(20,8) not null,
              currency varchar(8) not null,
              timestamp timestamptz not null,
              source text not null
            );
            create unique index if not exists ix_quote_cache_asset_id on quote_cache(asset_id);

            create table if not exists user_settings (
              owner_user_id uuid primary key references users(id) on delete cascade,
              display_name varchar(128) not null,
              role varchar(64) not null,
              login varchar(128) not null,
              preferred_currency varchar(8) not null,
              language varchar(16) not null,
              ui_scale numeric(10,4) not null,
              quote_provider varchar(64) not null,
              quote_refresh_minutes int not null,
              twelve_data_api_key_protected varchar(2048) not null,
              currency_provider varchar(64) not null
            );

            create table if not exists notifications (
              id uuid primary key,
              user_id uuid not null references users(id) on delete cascade,
              severity varchar(32) not null,
              title varchar(120) not null,
              message varchar(2000) not null,
              source varchar(120) not null,
              created_at_utc timestamptz not null,
              deleted_at_utc timestamptz null
            );
            create index if not exists ix_notifications_user_id_deleted_at_utc_created_at_utc on notifications(user_id, deleted_at_utc, created_at_utc desc);

            create table if not exists audit_log (
              id uuid primary key,
              user_id uuid null references users(id) on delete set null,
              action text not null,
              timestamp timestamptz not null,
              metadata_json text not null
            );
            create index if not exists ix_audit_log_timestamp on audit_log(timestamp desc);

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
        migrationBuilder.Sql(
            """
            drop table if exists audit_log cascade;
            drop table if exists notifications cascade;
            drop table if exists user_settings cascade;
            drop table if exists quote_cache cascade;
            drop table if exists goals cascade;
            drop table if exists asset_prices cascade;
            drop table if exists transactions cascade;
            drop table if exists asset_tags cascade;
            drop table if exists tags cascade;
            drop table if exists assets cascade;
            drop table if exists portfolios cascade;
            drop table if exists users cascade;
            """);
    }
}
