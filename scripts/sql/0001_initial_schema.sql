create table if not exists users (
  id uuid primary key,
  display_name text not null,
  login text not null unique,
  role text not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists portfolios (
  id uuid primary key,
  owner_user_id uuid not null references users(id) on delete restrict,
  name text not null,
  base_currency varchar(8) not null,
  is_archived boolean not null default false,
  created_at timestamptz not null,
  updated_at timestamptz not null
);
create index if not exists ix_portfolios_owner_archived on portfolios(owner_user_id, is_archived);

create table if not exists assets (
  id uuid primary key,
  portfolio_id uuid not null references portfolios(id) on delete restrict,
  ticker varchar(32) not null,
  name text not null,
  type text not null,
  currency varchar(8) not null,
  quantity numeric(20,8) not null,
  average_buy_price numeric(20,8) not null,
  current_price numeric(20,8) not null,
  is_archived boolean not null default false,
  created_at timestamptz not null,
  updated_at timestamptz not null
);
create index if not exists ix_assets_portfolio_archived on assets(portfolio_id, is_archived);

create table if not exists tags (
  id uuid primary key,
  name varchar(64) not null
);

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
  is_archived boolean not null default false
);
create index if not exists ix_transactions_portfolio_date on transactions(portfolio_id, trade_date desc);

create table if not exists asset_prices (
  id uuid primary key,
  asset_id uuid not null references assets(id) on delete cascade,
  price numeric(20,8) not null,
  currency varchar(8) not null,
  timestamp timestamptz not null
);
create index if not exists ix_asset_prices_asset_time on asset_prices(asset_id, timestamp desc);

create table if not exists goals (
  id uuid primary key,
  portfolio_id uuid not null references portfolios(id) on delete restrict,
  title text not null,
  target_amount numeric(20,8) not null,
  monthly_contribution numeric(20,8) not null,
  expected_annual_return_percent numeric(20,8) null,
  is_archived boolean not null default false,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists tax_profiles (
  id uuid primary key,
  user_id uuid not null references users(id) on delete restrict,
  profile_kind text not null,
  updated_at timestamptz not null
);

create table if not exists tax_reports (
  id uuid primary key,
  portfolio_id uuid not null references portfolios(id) on delete restrict,
  report_year int not null,
  taxable_base numeric(20,8) not null,
  total_tax_due numeric(20,8) not null,
  version text not null,
  created_at timestamptz not null
);

create table if not exists import_sessions (
  id uuid primary key,
  portfolio_id uuid not null references portfolios(id) on delete restrict,
  source text not null,
  started_at timestamptz not null
);

create table if not exists import_rows (
  id uuid primary key,
  import_session_id uuid not null references import_sessions(id) on delete cascade,
  status text not null,
  payload_json text not null
);

create table if not exists quote_cache (
  id uuid primary key,
  asset_id uuid not null unique,
  price numeric(20,8) not null,
  currency varchar(8) not null,
  timestamp timestamptz not null,
  source text not null
);

create table if not exists sync_snapshots (
  id uuid primary key,
  user_id uuid not null references users(id) on delete restrict,
  file_name text not null,
  created_at timestamptz not null
);

create table if not exists audit_log (
  id uuid primary key,
  user_id uuid null references users(id) on delete set null,
  action text not null,
  timestamp timestamptz not null,
  metadata_json text not null
);
create index if not exists ix_audit_log_timestamp on audit_log(timestamp desc);
