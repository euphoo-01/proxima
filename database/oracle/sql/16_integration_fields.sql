WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
  n PLS_INTEGER;
  PROCEDURE add_column(p_table VARCHAR2,p_column VARCHAR2,p_definition VARCHAR2) IS
  BEGIN
    SELECT COUNT(*) INTO n FROM all_tab_columns WHERE owner=SYS_CONTEXT('USERENV','CURRENT_SCHEMA')
      AND table_name=p_table AND column_name=p_column;
    IF n=0 THEN EXECUTE IMMEDIATE 'ALTER TABLE '||p_table||' ADD ('||p_column||' '||p_definition||')'; END IF;
  END;
  PROCEDURE add_constraint(p_table VARCHAR2,p_name VARCHAR2,p_definition VARCHAR2) IS
  BEGIN
    SELECT COUNT(*) INTO n FROM all_constraints WHERE owner=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND constraint_name=p_name;
    IF n=0 THEN EXECUTE IMMEDIATE 'ALTER TABLE '||p_table||' ADD CONSTRAINT '||p_name||' '||p_definition; END IF;
  END;
BEGIN
  add_column('USER_SETTINGS','RISK_FREE_RATE_PERCENT','NUMBER(12,6)');
  add_column('USER_SETTINGS','RISK_FREE_CURRENCY','VARCHAR2(3 CHAR)');
  add_column('IMPORT_BATCHES','FILE_HASH','RAW(32)');
  add_column('TRANSACTIONS','OPERATION_ORDER','NUMBER(18) DEFAULT 0 NOT NULL');
  add_constraint('IMPORT_BATCHES','UQ_IMPORT_BATCHES_FILE','UNIQUE (portfolio_id,file_hash)');
  add_constraint('TRANSACTIONS','CK_TRANSACTIONS_ORDER','CHECK (operation_order>=0 AND operation_order=TRUNC(operation_order))');
  add_constraint('USER_SETTINGS','FK_ER_34','FOREIGN KEY (risk_free_currency) REFERENCES currencies(code)');
  add_constraint('USER_SETTINGS','CK_SETTINGS_RISK',
    'CHECK ((risk_free_rate_percent IS NULL AND risk_free_currency IS NULL) OR '||
    '(risk_free_rate_percent IS NOT NULL AND risk_free_currency IS NOT NULL AND risk_free_rate_percent>-100))');
  SELECT COUNT(*) INTO n FROM all_indexes WHERE owner=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND index_name='UQ_TRANSACTIONS_ORDER';
  IF n=0 THEN
    EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX uq_transactions_order ON transactions(portfolio_id,SYS_EXTRACT_UTC(trade_date),operation_order)';
  END IF;
END;
/
