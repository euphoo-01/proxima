-- Run manually only if you need to repair the existing PostgreSQL database immediately.
-- The application patch runs the same cleanup automatically on startup.

alter table user_settings
  add column if not exists twelve_data_api_key_protected text not null default '';

alter table user_settings
  alter column twelve_data_api_key_protected set default '';

update user_settings
set quote_provider = 'TwelveData'
where quote_provider is null or quote_provider <> 'TwelveData';

alter table user_settings
  drop column if exists finnhub_api_key_protected;
