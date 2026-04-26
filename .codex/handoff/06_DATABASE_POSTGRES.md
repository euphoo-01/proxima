# Database — Local PostgreSQL

## Requirements

The app uses local PostgreSQL for durable storage.

Development setup:

- `docker-compose.yml` starts PostgreSQL.
- EF Core migrations create schema.
- Seed data creates demo user, portfolio, assets, prices, transactions and goals.

Production setup:

- app can connect to user-provided local PostgreSQL connection string;
- installer/docs explain local PostgreSQL dependency;
- secrets are not stored in repo.

## Technology

- EF Core
- Npgsql provider
- migrations committed to repo
- Testcontainers for integration tests if time allows

## Development Docker Compose

Create:

```yaml
services:
  postgres:
    image: postgres:17
    container_name: proxima-postgres
    environment:
      POSTGRES_DB: proxima
      POSTGRES_USER: proxima
      POSTGRES_PASSWORD: proxima_dev_password
    ports:
      - "5432:5432"
    volumes:
      - proxima_postgres_data:/var/lib/postgresql/data

volumes:
  proxima_postgres_data:
```

Never use dev password in production docs as a recommendation.

## Schema Draft

### users

- id uuid pk
- display_name text
- login text unique
- role text
- avatar_path text null
- preferred_currency text
- ui_scale numeric
- language text
- password_hash text
- password_salt text
- created_at timestamptz
- updated_at timestamptz

### portfolios

- id uuid pk
- user_id uuid fk
- name text
- base_currency text
- description text null
- is_archived bool
- created_at timestamptz
- updated_at timestamptz

### assets

- id uuid pk
- portfolio_id uuid fk
- ticker text
- name text
- type text
- currency text
- exchange text null
- isin text null
- notes_encrypted bytea null
- created_at timestamptz
- updated_at timestamptz

### tags

- id uuid pk
- portfolio_id uuid fk
- name text
- color text
- created_at timestamptz

### asset_tags

- asset_id uuid fk
- tag_id uuid fk
- pk(asset_id, tag_id)

### transactions

- id uuid pk
- portfolio_id uuid fk
- asset_id uuid fk null
- type text
- trade_date timestamptz
- quantity numeric
- price numeric
- gross_amount numeric
- fee_amount numeric
- tax_amount numeric
- currency text
- broker text null
- external_id text null
- notes_encrypted bytea null
- created_at timestamptz
- updated_at timestamptz

### asset_prices

- id uuid pk
- asset_id uuid fk
- timestamp timestamptz
- open numeric null
- high numeric null
- low numeric null
- close numeric
- volume numeric null
- source text
- created_at timestamptz

### goals

- id uuid pk
- portfolio_id uuid fk
- title text
- target_amount numeric
- target_currency text
- monthly_contribution numeric
- expected_annual_return numeric null
- target_date date null
- created_at timestamptz
- updated_at timestamptz

### tax_profiles

- id uuid pk
- user_id uuid fk
- country text
- legal_entity_type text
- residency_status text
- created_at timestamptz
- updated_at timestamptz

### tax_reports

- id uuid pk
- portfolio_id uuid fk
- tax_profile_id uuid fk
- year int
- status text
- taxable_base numeric
- total_tax_due numeric
- currency text
- calculation_version text
- generated_at timestamptz
- exported_pdf_path text null

### import_sessions

- id uuid pk
- portfolio_id uuid fk
- file_name text
- file_hash text
- status text
- started_at timestamptz
- completed_at timestamptz null
- error_message text null

### import_rows

- id uuid pk
- import_session_id uuid fk
- row_number int
- raw_payload_encrypted bytea
- parsed_payload jsonb
- status text
- confidence numeric
- validation_message text null

### quote_cache

- id uuid pk
- asset_id uuid fk
- provider text
- timestamp timestamptz
- price numeric
- currency text
- raw_payload jsonb null

### sync_snapshots

- id uuid pk
- user_id uuid fk
- snapshot_name text
- encrypted_file_path text
- checksum text
- created_at timestamptz
- uploaded_at timestamptz null
- provider text

### audit_log

- id uuid pk
- user_id uuid fk null
- event_type text
- entity_type text
- entity_id uuid null
- metadata jsonb
- created_at timestamptz

## Indexes

Required:

- transactions(portfolio_id, trade_date)
- transactions(asset_id, trade_date)
- asset_prices(asset_id, timestamp)
- assets(portfolio_id, ticker)
- tags(portfolio_id, name)
- tax_reports(portfolio_id, year)
- import_rows(import_session_id, status)

## Data Integrity

- Use FK constraints.
- Use decimal/numeric for money/quantity.
- Use timestamptz for timestamps.
- Use optimistic concurrency where helpful.
- Wrap import save in transaction.

## Migrations

Commit migrations. Provide commands:

```bash
dotnet ef migrations add InitialCreate -p src/Proxima.Infrastructure -s src/Proxima.App
dotnet ef database update -p src/Proxima.Infrastructure -s src/Proxima.App
```

## Seed Data

Seed:

- user: `demo`
- password: `demo123!` only for development/demo
- portfolio: “Основной портфель”
- assets: AAPL, MSFT, BTC, ETH, USD Cash, BYN Cash
- transactions over last 12 months
- prices over last 90 days
- two goals
- tax profile for физ. лицо РБ

## Important Sync Rule

Never sync PostgreSQL data directory through Google Drive. This can corrupt the database. For sync use encrypted application snapshots:

1. pause writes or create transaction-consistent export;
2. serialize selected data;
3. encrypt archive;
4. upload/copy encrypted archive;
5. import explicitly on other device.
