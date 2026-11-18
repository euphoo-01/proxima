
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

CREATE TABLE users (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  login VARCHAR2(255 CHAR) NOT NULL,
  display_name VARCHAR2(255 CHAR) NOT NULL,
  role VARCHAR2(255 CHAR) NOT NULL,
  password_hash VARCHAR2(512 CHAR) NOT NULL,
  failed_unlock_attempts NUMBER(10) DEFAULT 0 NOT NULL,
  account_status VARCHAR2(255 CHAR) NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE,
  CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE TABLE user_settings (
  owner_user_id RAW(16) NOT NULL,
  quote_provider_kind VARCHAR2(255 CHAR) NOT NULL,
  exchange_rate_provider_kind VARCHAR2(255 CHAR) NOT NULL,
  quote_api_key VARCHAR2(255 CHAR),
  CONSTRAINT pk_user_settings PRIMARY KEY (owner_user_id)
);

CREATE TABLE notifications (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  user_id RAW(16) NOT NULL,
  title VARCHAR2(255 CHAR) NOT NULL,
  message CLOB NOT NULL,
  source VARCHAR2(255 CHAR) NOT NULL,
  severity VARCHAR2(255 CHAR) NOT NULL,
  created_at_utc TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  deleted_at_utc TIMESTAMP WITH TIME ZONE,
  CONSTRAINT pk_notifications PRIMARY KEY (id)
);

CREATE TABLE audit_log (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  user_id RAW(16),
  action VARCHAR2(255 CHAR) NOT NULL,
  timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
  metadata_json CLOB NOT NULL,
  CONSTRAINT pk_audit_log PRIMARY KEY (id),
  CONSTRAINT ck_audit_log_metadata_json CHECK (metadata_json IS JSON)
);

CREATE TABLE tags (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  owner_user_id RAW(16) NOT NULL,
  name VARCHAR2(255 CHAR) NOT NULL,
  CONSTRAINT pk_tags PRIMARY KEY (id)
);

CREATE TABLE portfolios (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  owner_user_id RAW(16) NOT NULL,
  name VARCHAR2(255 CHAR) NOT NULL,
  description VARCHAR2(2000 CHAR),
  client_label VARCHAR2(255 CHAR),
  base_currency VARCHAR2(3 CHAR) NOT NULL,
  is_archived NUMBER(1) DEFAULT 0 NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  updated_at TIMESTAMP WITH TIME ZONE,
  CONSTRAINT pk_portfolios PRIMARY KEY (id),
  CONSTRAINT ck_portfolios_is_archived CHECK (is_archived IN (0,1))
);

CREATE TABLE import_batches (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  portfolio_id RAW(16) NOT NULL,
  imported_by_user_id RAW(16) NOT NULL,
  file_name VARCHAR2(255 CHAR) NOT NULL,
  status VARCHAR2(255 CHAR) NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  imported_count NUMBER(10) DEFAULT 0 NOT NULL,
  rejected_count NUMBER(10) DEFAULT 0 NOT NULL,
  CONSTRAINT pk_import_batches PRIMARY KEY (id)
);

CREATE TABLE import_rows (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  batch_id RAW(16) NOT NULL,
  row_number NUMBER(10) NOT NULL,
  raw_data CLOB NOT NULL,
  parsed_data CLOB,
  validation_status VARCHAR2(255 CHAR) NOT NULL,
  error_message VARCHAR2(2000 CHAR),
  CONSTRAINT pk_import_rows PRIMARY KEY (id),
  CONSTRAINT ck_import_rows_parsed_data CHECK (parsed_data IS JSON)
);

CREATE TABLE asset_tags (
  asset_id RAW(16) NOT NULL,
  tag_id RAW(16) NOT NULL,
  CONSTRAINT pk_asset_tags PRIMARY KEY (asset_id, tag_id)
);

CREATE TABLE assets (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  portfolio_id RAW(16) NOT NULL,
  instrument_id RAW(16) NOT NULL,
  quantity NUMBER(28,10) NOT NULL,
  average_buy_price NUMBER(28,10),
  current_price NUMBER(28,10),
  is_archived NUMBER(1) DEFAULT 0 NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  CONSTRAINT pk_assets PRIMARY KEY (id),
  CONSTRAINT ck_assets_is_archived CHECK (is_archived IN (0,1))
);

CREATE TABLE transactions (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  portfolio_id RAW(16) NOT NULL,
  asset_id RAW(16),
  import_batch_id RAW(16),
  type VARCHAR2(255 CHAR) NOT NULL,
  trade_date TIMESTAMP WITH TIME ZONE NOT NULL,
  quantity NUMBER(28,10) NOT NULL,
  price NUMBER(28,10) NOT NULL,
  gross_amount NUMBER(28,10) NOT NULL,
  currency VARCHAR2(3 CHAR) NOT NULL,
  fee_amount NUMBER(28,10) NOT NULL,
  tax_amount NUMBER(28,10) NOT NULL,
  broker VARCHAR2(255 CHAR),
  external_id VARCHAR2(255 CHAR),
  CONSTRAINT pk_transactions PRIMARY KEY (id)
);

CREATE TABLE goals (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  portfolio_id RAW(16) NOT NULL,
  title VARCHAR2(255 CHAR) NOT NULL,
  target_amount NUMBER(28,10) NOT NULL,
  currency VARCHAR2(3 CHAR) NOT NULL,
  monthly_contribution NUMBER(28,10) NOT NULL,
  expected_annual_return_percent NUMBER(12,6) NOT NULL,
  target_date DATE,
  is_archived NUMBER(1) DEFAULT 0 NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  CONSTRAINT pk_goals PRIMARY KEY (id),
  CONSTRAINT ck_goals_is_archived CHECK (is_archived IN (0,1))
);

CREATE TABLE portfolio_snapshots (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  portfolio_id RAW(16) NOT NULL,
  snapshot_date DATE NOT NULL,
  total_value NUMBER(28,10) NOT NULL,
  invested_amount NUMBER(28,10) NOT NULL,
  cash_balance NUMBER(28,10) NOT NULL,
  currency VARCHAR2(3 CHAR) NOT NULL,
  CONSTRAINT pk_portfolio_snapshots PRIMARY KEY (id)
);

CREATE TABLE instruments (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  ticker VARCHAR2(255 CHAR) NOT NULL,
  name VARCHAR2(255 CHAR) NOT NULL,
  type VARCHAR2(255 CHAR) NOT NULL,
  currency VARCHAR2(3 CHAR) NOT NULL,
  exchange VARCHAR2(255 CHAR),
  isin VARCHAR2(255 CHAR),
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  CONSTRAINT pk_instruments PRIMARY KEY (id)
);

CREATE TABLE asset_prices (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  instrument_id RAW(16) NOT NULL,
  price_time_utc TIMESTAMP WITH TIME ZONE NOT NULL,
  price NUMBER(28,10) NOT NULL,
  currency VARCHAR2(3 CHAR) NOT NULL,
  source VARCHAR2(255 CHAR) NOT NULL,
  CONSTRAINT pk_asset_prices PRIMARY KEY (id)
);

CREATE TABLE quote_cache (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  instrument_id RAW(16) NOT NULL,
  price NUMBER(28,10) NOT NULL,
  currency VARCHAR2(3 CHAR) NOT NULL,
  timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
  source VARCHAR2(255 CHAR) NOT NULL,
  CONSTRAINT pk_quote_cache PRIMARY KEY (id)
);

CREATE TABLE ml_results (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  training_run_id RAW(16) NOT NULL,
  instrument_id RAW(16) NOT NULL,
  result_type VARCHAR2(255 CHAR) NOT NULL,
  forecast_date DATE,
  predicted_price NUMBER(28,10),
  cluster_id NUMBER(10),
  cluster_distance NUMBER(28,10),
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  CONSTRAINT pk_ml_results PRIMARY KEY (id)
);

CREATE TABLE currencies (
  code VARCHAR2(3 CHAR) NOT NULL,
  name VARCHAR2(255 CHAR) NOT NULL,
  symbol VARCHAR2(255 CHAR) NOT NULL,
  CONSTRAINT pk_currencies PRIMARY KEY (code)
);

CREATE TABLE exchange_rates (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  rate_date DATE NOT NULL,
  from_currency VARCHAR2(3 CHAR) NOT NULL,
  to_currency VARCHAR2(3 CHAR) NOT NULL,
  rate NUMBER(28,10) NOT NULL,
  source VARCHAR2(255 CHAR) NOT NULL,
  CONSTRAINT pk_exchange_rates PRIMARY KEY (id)
);

CREATE TABLE ml_models (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  name VARCHAR2(255 CHAR) NOT NULL,
  task_type VARCHAR2(255 CHAR) NOT NULL,
  algorithm VARCHAR2(255 CHAR) NOT NULL,
  is_active NUMBER(1) DEFAULT 1 NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  CONSTRAINT pk_ml_models PRIMARY KEY (id),
  CONSTRAINT ck_ml_models_is_active CHECK (is_active IN (0,1))
);

CREATE TABLE ml_training_runs (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  model_id RAW(16) NOT NULL,
  instrument_id RAW(16),
  oracle_model_name VARCHAR2(255 CHAR),
  trained_at TIMESTAMP WITH TIME ZONE NOT NULL,
  training_rows NUMBER(10) NOT NULL,
  train_from DATE,
  train_to DATE,
  quality_metrics CLOB,
  status VARCHAR2(255 CHAR) NOT NULL,
  CONSTRAINT pk_ml_training_runs PRIMARY KEY (id),
  CONSTRAINT ck_ml_training_runs_quality_metrics CHECK (quality_metrics IS JSON)
);

ALTER TABLE users ADD CONSTRAINT uq_users_1 UNIQUE (login);

ALTER TABLE quote_cache ADD CONSTRAINT uq_quote_cache_1 UNIQUE (instrument_id);

ALTER TABLE assets ADD CONSTRAINT uq_assets_1 UNIQUE (portfolio_id, instrument_id);

ALTER TABLE assets ADD CONSTRAINT uq_assets_2 UNIQUE (id, portfolio_id);

CREATE UNIQUE INDEX uq_asset_prices_1 ON asset_prices (instrument_id, SYS_EXTRACT_UTC(price_time_utc), source);

ALTER TABLE exchange_rates ADD CONSTRAINT uq_exchange_rates_1 UNIQUE (rate_date, from_currency, to_currency, source);

ALTER TABLE portfolio_snapshots ADD CONSTRAINT uq_portfolio_snapshots_1 UNIQUE (portfolio_id, snapshot_date);

ALTER TABLE tags ADD CONSTRAINT uq_tags_1 UNIQUE (owner_user_id, name);

ALTER TABLE import_rows ADD CONSTRAINT uq_import_rows_1 UNIQUE (batch_id, row_number);

ALTER TABLE ml_training_runs ADD CONSTRAINT uq_ml_training_runs_1 UNIQUE (oracle_model_name);

ALTER TABLE user_settings ADD CONSTRAINT fk_er_01 FOREIGN KEY (owner_user_id) REFERENCES users (id);

ALTER TABLE portfolios ADD CONSTRAINT fk_er_02 FOREIGN KEY (owner_user_id) REFERENCES users (id);

CREATE INDEX ix_fk_er_02 ON portfolios (owner_user_id);

ALTER TABLE notifications ADD CONSTRAINT fk_er_03 FOREIGN KEY (user_id) REFERENCES users (id);

CREATE INDEX ix_fk_er_03 ON notifications (user_id);

ALTER TABLE audit_log ADD CONSTRAINT fk_er_04 FOREIGN KEY (user_id) REFERENCES users (id);

CREATE INDEX ix_fk_er_04 ON audit_log (user_id);

ALTER TABLE tags ADD CONSTRAINT fk_er_05 FOREIGN KEY (owner_user_id) REFERENCES users (id);

CREATE INDEX ix_fk_er_05 ON tags (owner_user_id);

ALTER TABLE import_batches ADD CONSTRAINT fk_er_06 FOREIGN KEY (imported_by_user_id) REFERENCES users (id);

CREATE INDEX ix_fk_er_06 ON import_batches (imported_by_user_id);

ALTER TABLE assets ADD CONSTRAINT fk_er_07 FOREIGN KEY (portfolio_id) REFERENCES portfolios (id);

CREATE INDEX ix_fk_er_07 ON assets (portfolio_id);

ALTER TABLE transactions ADD CONSTRAINT fk_er_08 FOREIGN KEY (portfolio_id) REFERENCES portfolios (id);

CREATE INDEX ix_fk_er_08 ON transactions (portfolio_id);

ALTER TABLE goals ADD CONSTRAINT fk_er_09 FOREIGN KEY (portfolio_id) REFERENCES portfolios (id);

CREATE INDEX ix_fk_er_09 ON goals (portfolio_id);

ALTER TABLE portfolio_snapshots ADD CONSTRAINT fk_er_10 FOREIGN KEY (portfolio_id) REFERENCES portfolios (id);

CREATE INDEX ix_fk_er_10 ON portfolio_snapshots (portfolio_id);

ALTER TABLE import_batches ADD CONSTRAINT fk_er_11 FOREIGN KEY (portfolio_id) REFERENCES portfolios (id);

CREATE INDEX ix_fk_er_11 ON import_batches (portfolio_id);

ALTER TABLE assets ADD CONSTRAINT fk_er_12 FOREIGN KEY (instrument_id) REFERENCES instruments (id);

CREATE INDEX ix_fk_er_12 ON assets (instrument_id);

ALTER TABLE asset_prices ADD CONSTRAINT fk_er_13 FOREIGN KEY (instrument_id) REFERENCES instruments (id);

CREATE INDEX ix_fk_er_13 ON asset_prices (instrument_id);

ALTER TABLE quote_cache ADD CONSTRAINT fk_er_14 FOREIGN KEY (instrument_id) REFERENCES instruments (id);


ALTER TABLE transactions ADD CONSTRAINT fk_er_15 FOREIGN KEY (asset_id) REFERENCES assets (id);

CREATE INDEX ix_fk_er_15 ON transactions (asset_id);

ALTER TABLE asset_tags ADD CONSTRAINT fk_er_16 FOREIGN KEY (asset_id) REFERENCES assets (id);

ALTER TABLE asset_tags ADD CONSTRAINT fk_er_17 FOREIGN KEY (tag_id) REFERENCES tags (id);

CREATE INDEX ix_fk_er_17 ON asset_tags (tag_id);

ALTER TABLE import_rows ADD CONSTRAINT fk_er_18 FOREIGN KEY (batch_id) REFERENCES import_batches (id);

CREATE INDEX ix_fk_er_18 ON import_rows (batch_id);

ALTER TABLE transactions ADD CONSTRAINT fk_er_19 FOREIGN KEY (import_batch_id) REFERENCES import_batches (id);

CREATE INDEX ix_fk_er_19 ON transactions (import_batch_id);

ALTER TABLE ml_training_runs ADD CONSTRAINT fk_er_20 FOREIGN KEY (model_id) REFERENCES ml_models (id);

CREATE INDEX ix_fk_er_20 ON ml_training_runs (model_id);

ALTER TABLE ml_training_runs ADD CONSTRAINT fk_er_21 FOREIGN KEY (instrument_id) REFERENCES instruments (id);

CREATE INDEX ix_fk_er_21 ON ml_training_runs (instrument_id);

ALTER TABLE ml_results ADD CONSTRAINT fk_er_22 FOREIGN KEY (training_run_id) REFERENCES ml_training_runs (id);

CREATE INDEX ix_fk_er_22 ON ml_results (training_run_id);

ALTER TABLE ml_results ADD CONSTRAINT fk_er_23 FOREIGN KEY (instrument_id) REFERENCES instruments (id);

CREATE INDEX ix_fk_er_23 ON ml_results (instrument_id);

ALTER TABLE exchange_rates ADD CONSTRAINT fk_er_24 FOREIGN KEY (from_currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_24 ON exchange_rates (from_currency);

ALTER TABLE exchange_rates ADD CONSTRAINT fk_er_25 FOREIGN KEY (to_currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_25 ON exchange_rates (to_currency);

ALTER TABLE instruments ADD CONSTRAINT fk_er_26 FOREIGN KEY (currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_26 ON instruments (currency);

ALTER TABLE asset_prices ADD CONSTRAINT fk_er_27 FOREIGN KEY (currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_27 ON asset_prices (currency);

ALTER TABLE quote_cache ADD CONSTRAINT fk_er_28 FOREIGN KEY (currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_28 ON quote_cache (currency);

ALTER TABLE portfolios ADD CONSTRAINT fk_er_29 FOREIGN KEY (base_currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_29 ON portfolios (base_currency);

ALTER TABLE goals ADD CONSTRAINT fk_er_30 FOREIGN KEY (currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_30 ON goals (currency);

ALTER TABLE transactions ADD CONSTRAINT fk_er_31 FOREIGN KEY (currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_31 ON transactions (currency);

ALTER TABLE portfolio_snapshots ADD CONSTRAINT fk_er_32 FOREIGN KEY (currency) REFERENCES currencies (code);

CREATE INDEX ix_fk_er_32 ON portfolio_snapshots (currency);

ALTER TABLE transactions ADD CONSTRAINT fk_transaction_asset_portfolio FOREIGN KEY (asset_id, portfolio_id) REFERENCES assets (id, portfolio_id);

ALTER TABLE exchange_rates ADD CONSTRAINT ck_exchange_rates_positive CHECK (rate > 0);
