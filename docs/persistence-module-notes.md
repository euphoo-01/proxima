# Module 17 Persistence Notes

## Implemented

- Added PostgreSQL dev setup:
  - `docker-compose.yml` with local Postgres 16.
- Added EF Core + Npgsql persistence foundation in `Proxima.Infrastructure`:
  - `ProximaDbContext` with explicit table mapping.
  - Decimal precision configured for money-like columns.
  - `DateTimeOffset` / `timestamptz`-oriented schema fields.
  - Dashboard-critical indexes for portfolio/transaction/time queries.
- Added bootstrap/config layer:
  - `DatabaseConnectionStringProvider` (`PROXIMA_DB_CONNECTION`, `PROXIMA_DB_SEED`).
  - `DatabaseBootstrapService` with graceful unavailable-DB message.
- Added committed SQL migration/seed scripts:
  - `scripts/sql/0001_initial_schema.sql`
  - `scripts/sql/0002_seed_demo.sql`

## Integrity Rules

- FK constraints and restrict delete paths are defined for critical relations.
- Asset cross-portfolio transaction validation remains enforced at service layer.
- Batch import rollback is still full-transactional at DB level only after repositories are switched from JSON to Postgres-backed implementations.

## Deferred

- Full runtime repository switch from JSON storage to PostgreSQL-backed repositories.
- EF migration artifacts generated via `dotnet ef` (currently schema is committed as SQL scripts).
- Full DB error/setup UX in-app with dedicated setup panel.
