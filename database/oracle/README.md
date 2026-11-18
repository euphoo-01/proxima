# Oracle bootstrap / public reference catalog

This first slice creates exactly the **21 tables, 148 fields and 32 relationships** in `docs/spec/er-model.drawio` (context: `docs/spec/previous-context.md`, sections 6–7). It adds no application table or column. It is not the completed financial application: authentication, user sessions, portfolio CRUD, financial posting, imports, calculations and ML are not implemented here. No actor ID accepted from a client is treated as authentication.

The only exposed API is `PROXIMA_OWNER.PKG_REFERENCE`, a definer-rights package exposing public catalog metadata and connection diagnostics. Each procedure has one `p_result OUT SYS_REFCURSOR` parameter:

| Procedure | Result columns |
|---|---|
| `get_server_info` | `database_name`, `session_user`, `server_time` (timestamp with time zone) |
| `list_currencies` | `code`, `name`, `symbol` |
| `list_instruments` | `instrument_id` (32 hex characters), `ticker`, `name`, `currency_code`, `instrument_type` |

Specifications precede bodies in `sql/20_api.sql`. The app account receives CREATE SESSION and EXECUTE on this package only; no SELECT/DML on tables. The owner is locked after bootstrap. SYS credentials are exclusively for provisioning. These are distinct local database accounts, not application users. No administrative application user is silently fabricated.

## Start from repository root

1. Install/start Docker Engine with Compose v2. Copy `.env.oracle.example` to `.env.oracle` and fill **three distinct** local passwords. The script deliberately permits only a leading ASCII letter followed by letters, digits, `_` or `#`, total length 12–64, to avoid SQL/connection interpolation. Keep this file local and out of version control.
2. Select a tag or digest compatible with your CPU from the [official Oracle Free repository](https://container-registry.oracle.com/ords/ocr/ba/database/free). The default `container-registry.oracle.com/database/free:latest` is the official Free image, but is mutable: record/set `ORACLE_IMAGE=...@sha256:...` for repeatable demonstrations. Oracle's repository documentation describes licensing/registry terms, platform support and image configuration. This slice uses FREEPDB1, AL32UTF8 and the image's SQL*Plus. Registry access may require accepting terms/login. The image can consume several GB of disk and RAM; first startup takes time.
3. Run `database/oracle/scripts/oracle.sh up`, then `database/oracle/scripts/oracle.sh bootstrap`, then `database/oracle/scripts/oracle.sh smoke`.
4. App connection: `localhost:1521/FREEPDB1`, user `PROXIMA_APP`, password from `ORACLE_APP_PASSWORD` (port may be changed with `ORACLE_PORT`). Never connect the app as SYS or owner.

Data is persisted in the `oracle_data` Compose volume; port binds to loopback. Bootstrap is intentionally fresh-schema-only and fails on existing users. It never drops users/tables or removes volumes. A failed DDL bootstrap may leave partially created objects because Oracle DDL commits implicitly; inspect and repair explicitly rather than rerunning destructive cleanup. Locked owner maintenance requires an explicit administrator unlock.

## Verification

`python3 database/oracle/scripts/verify_static.py` compares every physical table and ordered field against canonical ER XML and checks all 32 edge mappings. `sql/90_verify.sql` checks live tables, every column, 33 enabled/validated FKs (32 ER plus the documented transaction/portfolio consistency FK) and invalid objects during provisioning. SQL*Plus exits on SQL/OS error. `smoke` calls all three public procedures as the app account, asserts three returned currencies and checks absence of direct object privileges. Runtime smoke is required on the actual target; static validation alone does not prove successful Oracle compilation.

Seed: USD/EUR/BYN and AAPL/MSFT/SPY/BTC instrument metadata, deterministic instrument IDs. No users, trades, prices, exchange rates, performance history or predictions are fabricated. Reference instrument metadata is illustrative and must be reconciled with the chosen provider before market-data ingestion.

## Physical choices and remaining ER gaps

RAW(16) IDs default to SYS_GUID; currency codes use VARCHAR2(3 CHAR); quantity/money/rates use NUMBER(28,10), percentages NUMBER(12,6), boolean flags NUMBER(1), event times TIMESTAMP WITH TIME ZONE, calendar dates DATE. JSON-bearing CLOBs have IS JSON checks; raw import content is unrestricted CLOB. Date-only semantics and UTC normalization require future API validation. Nullability follows mandatory/optional diagram relationships; descriptive optional fields and unknown prices can remain NULL. Enumerations are intentionally not guessed beyond boolean flags: application role/status, transaction/import and ML values need a consistent approved contract before restricting them. All deletes default to RESTRICT semantics (no cascade).

Oracle rejects TIMESTAMP WITH TIME ZONE in UNIQUE constraints; asset-price uniqueness uses a function-based unique index over SYS_EXTRACT_UTC(price_time_utc), instrument_id and source, preserving the exact ER columns. Broker external IDs are VARCHAR2(255 CHAR); ML cluster IDs are NUMBER(10), not entity UUIDs.

Natural unique constraints implement the context's agreed uniqueness proposals; transactions additionally reference assets(id, portfolio_id) to prevent cross-portfolio assignment. Child FK indexes support joins and parent changes. No autonomous posting, cache synchronization or audit guarantees are claimed by mere table creation.

The ER has no session-token persistence/revocation contract, no explicit multi-currency cash account/FX legs, no operation audit retention/deletion strategy, no initial-balance provenance/FIFO lot representation, no import file digest, no central risk-free-rate parameters, no report snapshot entity and no explicit within-timestamp ordering field. These gaps affect agreed requirements in `docs/oracle-requirements.md`; none is patched by silently adding columns/tables. Password hashing, encryption/storage of quote_api_key, retention, transaction correction and authorization must be designed before exposing any private-data or write procedure. Existing timestamp fields preserve event time but do not settle ties. Precise enum values, numeric bounds and financial rules remain future work.
