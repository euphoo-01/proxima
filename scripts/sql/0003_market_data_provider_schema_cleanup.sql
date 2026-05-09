-- Idempotent schema cleanup after moving market data integration to Twelve Data.
-- This script repairs already-created PostgreSQL databases that still contain
-- legacy provider columns with NOT NULL constraints.

alter table user_settings
  add column if not exists twelve_data_api_key_protected text not null default '';

alter table user_settings
  alter column twelve_data_api_key_protected set default '';

update user_settings
set quote_provider = 'TwelveData'
where quote_provider is null or quote_provider <> 'TwelveData';

do $$
begin
  if exists (
    select 1
    from information_schema.columns
    where table_schema = 'public'
      and table_name = 'user_settings'
      and column_name = 'finnhub_api_key_protected'
  ) then
    alter table user_settings drop column finnhub_api_key_protected;
  end if;
end $$;
