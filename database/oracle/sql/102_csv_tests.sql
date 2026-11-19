WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET DEFINE OFF
DECLARE
  user_id_value RAW(16):=SYS_GUID(); instrument_id_value RAW(16):=SYS_GUID(); token_value VARCHAR2(64):=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32));
  p1 RAW(16); p2 RAW(16); batch_value RAW(16); other_batch RAW(16); invalid_batch RAW(16);
  content_value CLOB; exported CLOB; n NUMBER; amount_value NUMBER; ticker_value VARCHAR2(40):='CSV_'||SUBSTR(RAWTOHEX(SYS_GUID()),1,12);
  c_header VARCHAR2(200):='trade_date,ticker,name,type,quantity,price,currency,fee,amount,operation_order';
  PROCEDURE assert_true(p_value BOOLEAN,p_name VARCHAR2) IS
  BEGIN
    IF p_value IS NULL OR NOT p_value THEN RAISE_APPLICATION_ERROR(-20690,p_name); END IF;
    DBMS_OUTPUT.PUT_LINE('PASS '||p_name);
  END;
BEGIN
  SAVEPOINT csv_fixture;
  INSERT INTO users(id,login,display_name,role,password_hash,account_status)
    VALUES(user_id_value,'csv_'||LOWER(RAWTOHEX(user_id_value)),'CSV fixture','PrivateInvestor','fixture-not-a-password-hash','Active');
  INSERT INTO auth_sessions(id,user_id,token_hash,created_at,expires_at)
    VALUES(SYS_GUID(),user_id_value,DBMS_CRYPTO.HASH(HEXTORAW(token_value),DBMS_CRYPTO.HASH_SH256),SYSTIMESTAMP,SYSTIMESTAMP+INTERVAL '1' HOUR);
  INSERT INTO instruments(id,ticker,name,type,currency)
    VALUES(instrument_id_value,ticker_value,'Acme, "Quoted"'||CHR(10)||'Holdings','Stock','USD');
  pkg_portfolios.create_portfolio(token_value,'CSV first','USD',NULL,NULL,p1);
  pkg_portfolios.create_portfolio(token_value,'CSV roundtrip','USD',NULL,NULL,p2);
  content_value:=UNISTR('\FEFF')||c_header||CHR(13)||CHR(10)||
    '2024-01-02T00:00:00Z,'||ticker_value||',"Acme, ""Quoted""'||CHR(10)||'Holdings",Sell,1,120,USD,2,120,0'||CHR(13)||CHR(10)||
    '2024-01-01T00:00:00Z,'||ticker_value||',Buy label,Buy,2,100,USD,1,200,1'||CHR(13)||CHR(10)||
    '2024-01-01T00:00:00Z,,,Deposit,0,0,USD,0,1000,0'||CHR(13)||CHR(10);
  pkg_csv.preview(token_value,p1,'fixture.csv',content_value,batch_value);
  SELECT COUNT(*) INTO n FROM import_rows WHERE batch_id=batch_value AND validation_status='Valid';
  assert_true(n=3,'preview_parses_bom_quoted_comma_quote_newline');
  SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p1;
  assert_true(n=0,'preview_has_no_financial_writes');
  BEGIN
    pkg_csv.preview(token_value,p1,'renamed.csv',content_value,other_batch);
    RAISE_APPLICATION_ERROR(-20691,'duplicate_hash_accepted');
  EXCEPTION WHEN OTHERS THEN IF SQLCODE<>-20601 THEN RAISE; END IF; END;
  DBMS_OUTPUT.PUT_LINE('PASS original_file_hash_duplicate_rejected');
  SELECT COUNT(*) INTO amount_value FROM audit_log WHERE user_id=user_id_value;
  pkg_csv.validate_batch(token_value,batch_value);
  SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p1;
  assert_true(n=0,'validation_rolls_back_simulation_transactions');
  SELECT COUNT(*) INTO n FROM assets WHERE portfolio_id=p1;
  assert_true(n=0,'validation_rolls_back_simulation_assets');
  SELECT COUNT(*) INTO n FROM audit_log WHERE user_id=user_id_value;
  assert_true(n=amount_value,'validation_has_no_audit_side_effect');
  pkg_csv.commit_batch(token_value,batch_value);
  SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p1;
  assert_true(n=3,'commit_inserts_entire_unsorted_batch_before_replay');
  SELECT quantity INTO n FROM assets WHERE portfolio_id=p1;
  assert_true(n=1,'commit_derives_position_quantity');
  SELECT average_buy_price INTO amount_value FROM assets WHERE portfolio_id=p1;
  assert_true(amount_value=100.5,'commit_derives_fifo_remaining_basis');
  SELECT SUM(CASE WHEN type IN ('Deposit','Sell') THEN gross_amount ELSE -gross_amount END-fee_amount)
    INTO amount_value FROM transactions WHERE portfolio_id=p1;
  assert_true(amount_value=917,'commit_derives_cash_with_fees');
  SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p1 AND trade_date=TO_TIMESTAMP_TZ('2024-01-01 00:00:00 +00:00','YYYY-MM-DD HH24:MI:SS TZH:TZM');
  assert_true(n=2,'same_time_order_preserves_original_timestamp');
  BEGIN
    pkg_csv.correct_row(token_value,batch_value,1,'bad');
    RAISE_APPLICATION_ERROR(-20692,'imported_batch_edit_accepted');
  EXCEPTION WHEN OTHERS THEN IF SQLCODE<>-20600 THEN RAISE; END IF; END;
  DBMS_OUTPUT.PUT_LINE('PASS imported_batch_is_immutable');
  pkg_csv.export_transactions(token_value,p1,exported);
  assert_true(DBMS_LOB.INSTR(exported,'"Acme, ""Quoted""'||CHR(10)||'Holdings"')>0,'export_escapes_catalog_metadata');
  pkg_csv.preview(token_value,p2,'roundtrip.csv',exported,other_batch);
  pkg_csv.validate_batch(token_value,other_batch); pkg_csv.commit_batch(token_value,other_batch);
  SELECT SUM(CASE WHEN type IN ('Deposit','Sell') THEN gross_amount ELSE -gross_amount END-fee_amount)
    INTO amount_value FROM transactions WHERE portfolio_id=p2;
  assert_true(amount_value=917,'roundtrip_preserves_cash');
  SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p2 AND operation_order=1;
  assert_true(n=1,'roundtrip_preserves_operation_order');
  content_value:=c_header||CHR(10)||'2024-01-03T00:00:00Z,'||ticker_value||',Oversell,Sell,99,120,USD,0,11880,0';
  pkg_csv.preview(token_value,p1,'invalid-history.csv',content_value,invalid_batch);
  pkg_csv.validate_batch(token_value,invalid_batch);
  SELECT COUNT(*) INTO n FROM import_rows WHERE batch_id=invalid_batch AND validation_status='Invalid' AND error_message LIKE 'Batch history validation failed%';
  assert_true(n=1,'invalid_history_has_honest_batch_error');
  BEGIN
    pkg_csv.commit_batch(token_value,invalid_batch);
    RAISE_APPLICATION_ERROR(-20693,'invalid_batch_committed');
  EXCEPTION WHEN OTHERS THEN IF SQLCODE<>-20108 THEN RAISE; END IF; END;
  SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p1;
  assert_true(n=3,'invalid_commit_rolls_back_all_financial_writes');
  content_value:=c_header||CHR(10)||'2024-01-03T00:00:00Z,,,Deposit,0,0,USD,0,20,0';
  pkg_csv.correct_row(token_value,invalid_batch,1,SUBSTR(content_value,LENGTH(c_header)+2));
  pkg_csv.validate_batch(token_value,invalid_batch); pkg_csv.commit_batch(token_value,invalid_batch);
  SELECT imported_count INTO n FROM import_batches WHERE id=invalid_batch;
  assert_true(n=1,'corrected_batch_can_validate_and_import');
  content_value:=c_header||CHR(10)||'2024-01-04T00:00:00Z,"unclosed';
  pkg_csv.preview(token_value,p1,'malformed.csv',content_value,other_batch);
  SELECT COUNT(*) INTO n FROM import_rows WHERE batch_id=other_batch AND validation_status='Invalid';
  assert_true(n=1,'malformed_record_is_staged_with_error');
  ROLLBACK TO csv_fixture;
  IF DBMS_LOB.ISTEMPORARY(exported)=1 THEN DBMS_LOB.FREETEMPORARY(exported); END IF;
EXCEPTION WHEN OTHERS THEN
  ROLLBACK TO csv_fixture;
  IF DBMS_LOB.ISTEMPORARY(exported)=1 THEN DBMS_LOB.FREETEMPORARY(exported); END IF;
  RAISE;
END;
/
