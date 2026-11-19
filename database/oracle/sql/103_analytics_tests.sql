WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET DEFINE OFF
ALTER SESSION SET CURRENT_SCHEMA=PROXIMA_OWNER;
DECLARE
  investor RAW(16):=SYS_GUID(); foreign_user RAW(16):=SYS_GUID(); administrator RAW(16):=SYS_GUID();
  token_value VARCHAR2(64):=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32));
  foreign_token VARCHAR2(64):=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32));
  admin_token VARCHAR2(64):=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32));
  portfolio_value RAW(16):=SYS_GUID(); fx_portfolio RAW(16):=SYS_GUID(); asset_value RAW(16):=SYS_GUID();
  instrument_value RAW(16); missing_instrument RAW(16):=SYS_GUID(); cursor_value SYS_REFCURSOR; count_value NUMBER; error_value NUMBER;
  t1 TIMESTAMP WITH TIME ZONE:=TO_TIMESTAMP_TZ('2024-01-01 20:00:00 UTC','YYYY-MM-DD HH24:MI:SS TZR');
  t2 TIMESTAMP WITH TIME ZONE:=TO_TIMESTAMP_TZ('2024-01-02 20:00:00 UTC','YYYY-MM-DD HH24:MI:SS TZR');
  t3 TIMESTAMP WITH TIME ZONE:=TO_TIMESTAMP_TZ('2024-01-03 20:00:00 UTC','YYYY-MM-DD HH24:MI:SS TZR');
  asof_value TIMESTAMP WITH TIME ZONE:=TO_TIMESTAMP_TZ('2024-01-04 20:00:00 UTC','YYYY-MM-DD HH24:MI:SS TZR');
  PROCEDURE check_true(p_ok BOOLEAN,p_name VARCHAR2) IS
  BEGIN
    IF p_ok IS NULL OR NOT p_ok THEN RAISE_APPLICATION_ERROR(-20991,p_name); END IF;
    DBMS_OUTPUT.PUT_LINE('PASS '||p_name);
  END;
  PROCEDURE fixture_user(p_id RAW,p_role VARCHAR2,p_token VARCHAR2) IS
  BEGIN
    INSERT INTO users(id,login,display_name,role,password_hash,account_status)
      VALUES(p_id,'analytics_'||RAWTOHEX(p_id),'Analytics fixture',p_role,'unusable-fixture-password','Active');
    INSERT INTO auth_sessions(user_id,token_hash,expires_at)
      VALUES(p_id,DBMS_CRYPTO.HASH(HEXTORAW(p_token),DBMS_CRYPTO.HASH_SH256),SYSTIMESTAMP+INTERVAL '1' HOUR);
  END;
  PROCEDURE snapshot(p_portfolio RAW,p_expected_total NUMBER,p_expected_pnl NUMBER,p_missing NUMBER,p_name VARCHAR2) IS
    base_value VARCHAR2(3); time_value TIMESTAMP WITH TIME ZONE; total_value NUMBER; cash_value NUMBER; position_value NUMBER;
    external_value NUMBER; pnl_value NUMBER; roi_value NUMBER; missing_value NUMBER; issues CLOB; detail CLOB;
  BEGIN
    pkg_analytics.snapshot_as_of(token_value,p_portfolio,asof_value,cursor_value);
    FETCH cursor_value INTO base_value,time_value,total_value,cash_value,position_value,external_value,pnl_value,roi_value,missing_value,issues,detail;
    check_true(cursor_value%FOUND AND base_value='USD' AND missing_value=p_missing,p_name||'_metadata');
    IF p_expected_total IS NULL THEN
      check_true(total_value IS NULL AND pnl_value IS NULL AND roi_value IS NULL AND DBMS_LOB.GETLENGTH(issues)>2,p_name||'_missing_is_not_zero');
    ELSE
      check_true(ABS(total_value-p_expected_total)<0.000001 AND ABS(pnl_value-p_expected_pnl)<0.000001,p_name||'_values');
      check_true(total_value=cash_value+position_value AND ABS(roi_value-100*pnl_value/external_value)<0.000001,p_name||'_accounting_identity');
      check_true(DBMS_LOB.GETLENGTH(detail)>2,p_name||'_provenance');
    END IF;
    CLOSE cursor_value;
  END;
  PROCEDURE reject_case(p_case PLS_INTEGER,p_expected NUMBER,p_name VARCHAR2) IS id_value RAW(16);
  BEGIN
    error_value:=0;
    BEGIN
      CASE p_case
        WHEN 1 THEN pkg_market.put_price(token_value,instrument_value,t1,100,'USD','A_Source');
        WHEN 2 THEN pkg_market.put_price(admin_token,instrument_value,t1,0,'USD','A_Source');
        WHEN 3 THEN pkg_market.put_price(admin_token,instrument_value,t1,100,'EUR','A_Source');
        WHEN 4 THEN pkg_market.put_exchange_rate(admin_token,DATE '2024-01-01','USD','EUR',0,'FX');
        WHEN 5 THEN pkg_market.put_exchange_rate(admin_token,DATE '2024-01-01','USD','USD',2,'FX');
        WHEN 6 THEN pkg_analytics.snapshot_as_of(foreign_token,portfolio_value,asof_value,cursor_value);
        WHEN 7 THEN pkg_analytics.snapshot_as_of(admin_token,portfolio_value,asof_value,cursor_value);
        WHEN 8 THEN pkg_analytics.instrument_risk(token_value,instrument_value,DATE '2024-01-01',DATE '2024-01-03','A_Source',0,0,cursor_value);
        WHEN 9 THEN pkg_analytics.instrument_risk(token_value,instrument_value,DATE '2024-01-01',DATE '2024-01-03','A_Source',252,-100,cursor_value);
        WHEN 10 THEN pkg_market.get_price_history('invalid',instrument_value,t1,t3,cursor_value);
        WHEN 11 THEN id_value:=instrument_value; pkg_market.save_instrument(admin_token,id_value,'ANALYTICS','Changed currency','Stock','EUR','Fixture',NULL);
      END CASE;
    EXCEPTION WHEN OTHERS THEN error_value:=SQLCODE; END;
    check_true(error_value=p_expected,p_name);
  END;
BEGIN
  check_true(SYS_CONTEXT('USERENV','SESSION_USER')='SYS' AND SYS_CONTEXT('USERENV','CON_NAME')<>'CDB$ROOT','analytics_trusted_fixture_session');
  SAVEPOINT analytics_fixtures;
  fixture_user(investor,'PrivateInvestor',token_value);
  fixture_user(foreign_user,'FinancialConsultant',foreign_token);
  fixture_user(administrator,'Admin',admin_token);
  pkg_workspace.save_settings(token_value,'Alpaca','Nbrb',0,'USD');
  pkg_market.save_instrument(admin_token,instrument_value,'ANALYTICS','Analytics fixture','Stock','USD','Fixture',NULL);
  pkg_market.put_price(admin_token,instrument_value,t1,100,'USD','A_Source');
  pkg_market.put_price(admin_token,instrument_value,t2,110,'USD','A_Source');
  pkg_market.put_price(admin_token,instrument_value,t3,98,'USD','A_Source');
  pkg_market.put_price(admin_token,instrument_value,t3,99,'USD','A_Source');
  pkg_market.put_price(admin_token,instrument_value,t3,999,'USD','Z_Source');
  SELECT COUNT(*) INTO count_value FROM asset_prices WHERE instrument_id=instrument_value;
  check_true(count_value=4,'market_upsert_preserves_sources_without_duplicate_same_source');
  SELECT COUNT(*) INTO count_value FROM quote_cache WHERE instrument_id=instrument_value AND price=99 AND source='A_Source';
  check_true(count_value=1,'market_cache_tie_uses_binary_source_order');
  pkg_market.put_price(admin_token,instrument_value,t1,100,'USD','A_Source');
  SELECT COUNT(*) INTO count_value FROM quote_cache WHERE instrument_id=instrument_value AND price=99;
  check_true(count_value=1,'market_backfill_does_not_rewind_cache');
  reject_case(1,-20302,'market_write_requires_admin');
  reject_case(2,-20700,'market_price_must_be_positive');
  reject_case(3,-20700,'market_price_currency_must_match_instrument');
  reject_case(4,-20700,'market_rate_must_be_positive');
  reject_case(5,-20700,'market_identity_rate_is_one');
  reject_case(10,-20301,'market_history_requires_session');
  reject_case(11,-20700,'market_instrument_currency_cannot_be_rewritten');
  pkg_market.get_price_history(token_value,instrument_value,t1,t3,cursor_value); CLOSE cursor_value;

  INSERT INTO portfolios(id,owner_user_id,name,base_currency) VALUES(portfolio_value,investor,'Analytics','USD');
  INSERT INTO portfolios(id,owner_user_id,name,base_currency) VALUES(fx_portfolio,investor,'FX analytics','USD');
  INSERT INTO assets(id,portfolio_id,instrument_id,quantity,average_buy_price)
    VALUES(asset_value,portfolio_value,instrument_value,999,999);
  INSERT INTO transactions(portfolio_id,type,trade_date,quantity,price,gross_amount,currency,fee_amount,tax_amount,operation_order)
    VALUES(portfolio_value,'Deposit',t1-INTERVAL '2' HOUR,0,0,1000,'USD',2,0,0);
  INSERT INTO transactions(portfolio_id,asset_id,type,trade_date,quantity,price,gross_amount,currency,fee_amount,tax_amount,operation_order)
    VALUES(portfolio_value,asset_value,'Buy',t1-INTERVAL '1' HOUR,5,100,500,'USD',3,0,0);
  snapshot(portfolio_value,990,-10,0,'analytics_trade_history_not_current_aggregates');
  reject_case(6,-20722,'analytics_foreign_portfolio_is_unavailable');
  reject_case(7,-20720,'analytics_admin_has_no_owner_bypass');
  pkg_market.put_price(admin_token,instrument_value,asof_value+INTERVAL '1' DAY,500,'USD','A_Source');
  snapshot(portfolio_value,990,-10,0,'analytics_price_after_valuation_is_excluded');
  DELETE FROM asset_prices WHERE instrument_id=instrument_value;
  snapshot(portfolio_value,NULL,NULL,1,'analytics_missing_price_is_explicit');
  pkg_market.put_price(admin_token,instrument_value,t1,100,'USD','A_Source');
  pkg_market.put_price(admin_token,instrument_value,t2,110,'USD','A_Source');
  pkg_market.put_price(admin_token,instrument_value,t3,99,'USD','A_Source');

  INSERT INTO transactions(portfolio_id,type,trade_date,quantity,price,gross_amount,currency,fee_amount,tax_amount,operation_order)
    VALUES(fx_portfolio,'Deposit',t1,0,0,100,'EUR',0,0,0);
  DELETE FROM exchange_rates WHERE from_currency IN ('USD','EUR') OR to_currency IN ('USD','EUR');
  snapshot(fx_portfolio,NULL,NULL,2,'analytics_missing_current_and_historical_fx');
  pkg_market.put_exchange_rate(admin_token,DATE '2024-01-01','EUR','BYN',3.2,'A_FX');
  pkg_market.put_exchange_rate(admin_token,DATE '2024-01-01','USD','BYN',2.5,'A_FX');
  pkg_market.put_exchange_rate(admin_token,DATE '2024-01-03','USD','BYN',3,'A_FX');
  pkg_market.put_exchange_rate(admin_token,DATE '2024-01-10','EUR','BYN',99,'A_FX');
  snapshot(fx_portfolio,100*3.2/3,100*3.2/3-128,0,'analytics_byn_cross_uses_historical_flow_dates');
  pkg_market.put_exchange_rate(admin_token,DATE '2024-01-02','EUR','USD',1.2,'A_FX');
  pkg_market.put_exchange_rate(admin_token,DATE '2024-01-03','USD','EUR',0.5,'A_FX');
  snapshot(fx_portfolio,200,72,0,'analytics_newer_inverse_beats_older_direct');
  pkg_market.put_exchange_rate(admin_token,DATE '2024-01-03','EUR','USD',1.5,'A_FX');
  snapshot(fx_portfolio,150,22,0,'analytics_direct_wins_same_date_inverse');

  DECLARE
    prices NUMBER; returns_value NUMBER; first_value DATE; last_value DATE; source_value VARCHAR2(255);
    periods NUMBER; rf NUMBER; volatility NUMBER; sharpe NUMBER; mdd NUMBER; gap NUMBER; status_value VARCHAR2(64); risk_status VARCHAR2(64);
  BEGIN
    pkg_analytics.instrument_risk(token_value,instrument_value,DATE '2024-01-01',DATE '2024-01-03','A_Source',252,0,cursor_value);
    FETCH cursor_value INTO prices,returns_value,first_value,last_value,source_value,periods,rf,volatility,sharpe,mdd,gap,status_value,risk_status;
    check_true(prices=3 AND returns_value=2 AND source_value='A_Source' AND periods=252 AND gap=1 AND risk_status='CONFIGURED','analytics_risk_explicit_series_metadata');
    check_true(ABS(volatility-SQRT(0.02)*SQRT(252))<0.000001 AND ABS(sharpe)<0.000001 AND ABS(mdd-0.1)<0.000001,
      'analytics_sample_volatility_sharpe_and_drawdown'); CLOSE cursor_value;
    pkg_analytics.instrument_risk(token_value,instrument_value,DATE '2024-01-01',DATE '2024-01-01','A_Source',252,0,cursor_value);
    FETCH cursor_value INTO prices,returns_value,first_value,last_value,source_value,periods,rf,volatility,sharpe,mdd,gap,status_value,risk_status;
    check_true(status_value='INSUFFICIENT_DATA' AND volatility IS NULL AND sharpe IS NULL AND mdd IS NULL,
      'analytics_insufficient_risk_data_is_not_zero'); CLOSE cursor_value;
    pkg_workspace.save_settings(token_value,'Alpaca','Nbrb');
    pkg_analytics.instrument_risk(token_value,instrument_value,DATE '2024-01-01',DATE '2024-01-03','A_Source',252,NULL,cursor_value);
    FETCH cursor_value INTO prices,returns_value,first_value,last_value,source_value,periods,rf,volatility,sharpe,mdd,gap,status_value,risk_status;
    check_true(risk_status='MISSING_RISK_FREE_RATE' AND sharpe IS NULL AND rf IS NULL AND volatility>0 AND mdd>0,
      'analytics_missing_configured_rate_does_not_hide_other_metrics'); CLOSE cursor_value;
    pkg_workspace.save_settings(token_value,'Alpaca','Nbrb',5,'EUR');
    pkg_analytics.instrument_risk(token_value,instrument_value,DATE '2024-01-01',DATE '2024-01-03','A_Source',252,NULL,cursor_value);
    FETCH cursor_value INTO prices,returns_value,first_value,last_value,source_value,periods,rf,volatility,sharpe,mdd,gap,status_value,risk_status;
    check_true(risk_status='RISK_FREE_CURRENCY_MISMATCH' AND sharpe IS NULL AND volatility>0,
      'analytics_risk_free_currency_must_match_instrument'); CLOSE cursor_value;
  END;
  reject_case(8,-20721,'analytics_risk_requires_explicit_supported_frequency');
  reject_case(9,-20721,'analytics_risk_rejects_minus_100_percent');
  ROLLBACK TO analytics_fixtures;
  SELECT COUNT(*) INTO count_value FROM instruments WHERE id=instrument_value;
  check_true(count_value=0,'market_and_analytics_do_not_commit_caller_transaction');
EXCEPTION WHEN OTHERS THEN ROLLBACK; RAISE;
END;
/
