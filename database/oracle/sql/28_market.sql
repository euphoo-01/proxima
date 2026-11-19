WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_market AUTHID DEFINER AS
  PROCEDURE save_instrument(p_token VARCHAR2,p_id IN OUT RAW,p_ticker VARCHAR2,p_name VARCHAR2,
    p_type VARCHAR2,p_currency VARCHAR2,p_exchange VARCHAR2,p_isin VARCHAR2);
  PROCEDURE put_price(p_token VARCHAR2,p_instrument_id RAW,p_price_time TIMESTAMP WITH TIME ZONE,
    p_price NUMBER,p_currency VARCHAR2,p_source VARCHAR2);
  PROCEDURE put_exchange_rate(p_token VARCHAR2,p_rate_date DATE,p_from_currency VARCHAR2,
    p_to_currency VARCHAR2,p_rate NUMBER,p_source VARCHAR2);
  PROCEDURE get_price_history(p_token VARCHAR2,p_instrument_id RAW,p_from_time TIMESTAMP WITH TIME ZONE,
    p_to_time TIMESTAMP WITH TIME ZONE,p_result OUT SYS_REFCURSOR);
END pkg_market;
/
CREATE OR REPLACE PACKAGE BODY pkg_market AS
  PROCEDURE audit_event(p_user RAW,p_action VARCHAR2,p_id RAW,p_source VARCHAR2) IS
  BEGIN
    INSERT INTO audit_log(user_id,action,timestamp,metadata_json)
      VALUES(p_user,p_action,SYSTIMESTAMP,JSON_OBJECT('instrument_id' VALUE RAWTOHEX(p_id),'source' VALUE p_source RETURNING CLOB));
  END;
  PROCEDURE currency_exists(p_currency VARCHAR2) IS n NUMBER;
  BEGIN
    SELECT COUNT(*) INTO n FROM currencies WHERE code=p_currency;
    IF n<>1 THEN RAISE_APPLICATION_ERROR(-20700,'Unknown currency'); END IF;
  END;
  PROCEDURE positive_number(p_value NUMBER) IS
  BEGIN
    IF p_value IS NULL OR ROUND(p_value,10)<=0 OR ROUND(p_value,10)>=POWER(10,18) THEN
      RAISE_APPLICATION_ERROR(-20700,'Value must be positive and fit NUMBER(28,10)');
    END IF;
  END;
  PROCEDURE save_instrument(p_token VARCHAR2,p_id IN OUT RAW,p_ticker VARCHAR2,p_name VARCHAR2,
    p_type VARCHAR2,p_currency VARCHAR2,p_exchange VARCHAR2,p_isin VARCHAR2) IS
    user_value RAW(16); old_id RAW(16):=p_id; currency_value VARCHAR2(3); previous_currency VARCHAR2(3);
  BEGIN
    SAVEPOINT market_api; user_value:=pkg_auth.require_admin(p_token);
    IF TRIM(p_ticker) IS NULL OR LENGTH(TRIM(p_ticker))>255 OR TRIM(p_name) IS NULL OR LENGTH(TRIM(p_name))>255
      OR p_type IS NULL OR p_type NOT IN ('Stock','ETF','Cryptocurrency') OR LENGTH(TRIM(p_currency))<>3
      OR TRIM(p_currency) IS NULL OR LENGTH(p_exchange)>255 OR LENGTH(p_isin)>255 THEN
      RAISE_APPLICATION_ERROR(-20700,'Invalid instrument parameters');
    END IF;
    currency_value:=UPPER(TRIM(p_currency)); currency_exists(currency_value);
    IF p_id IS NULL THEN
      p_id:=SYS_GUID();
      INSERT INTO instruments(id,ticker,name,type,currency,exchange,isin)
        VALUES(p_id,UPPER(TRIM(p_ticker)),TRIM(p_name),p_type,currency_value,TRIM(p_exchange),TRIM(p_isin));
    ELSE
      SELECT currency INTO previous_currency FROM instruments WHERE id=p_id FOR UPDATE;
      IF previous_currency<>currency_value THEN RAISE_APPLICATION_ERROR(-20700,'Existing instrument currency is immutable'); END IF;
      UPDATE instruments SET ticker=UPPER(TRIM(p_ticker)),name=TRIM(p_name),type=p_type,
        exchange=TRIM(p_exchange),isin=TRIM(p_isin) WHERE id=p_id;
    END IF;
    audit_event(user_value,'InstrumentSaved',p_id,NULL);
  EXCEPTION
    WHEN NO_DATA_FOUND THEN ROLLBACK TO market_api; p_id:=old_id; RAISE_APPLICATION_ERROR(-20701,'Instrument unavailable');
    WHEN OTHERS THEN ROLLBACK TO market_api; p_id:=old_id; RAISE;
  END;
  PROCEDURE put_price(p_token VARCHAR2,p_instrument_id RAW,p_price_time TIMESTAMP WITH TIME ZONE,
    p_price NUMBER,p_currency VARCHAR2,p_source VARCHAR2) IS
    user_value RAW(16); currency_value VARCHAR2(3); id_value RAW(16); source_value VARCHAR2(255);
    latest_price NUMBER; latest_time TIMESTAMP WITH TIME ZONE; latest_source VARCHAR2(255);
  BEGIN
    SAVEPOINT market_api; user_value:=pkg_auth.require_admin(p_token); positive_number(p_price);
    IF p_price_time IS NULL OR p_price_time>SYSTIMESTAMP OR TRIM(p_source) IS NULL OR LENGTH(TRIM(p_source))>255 THEN
      RAISE_APPLICATION_ERROR(-20700,'Nonfuture quote time and source required');
    END IF;
    SELECT currency INTO currency_value FROM instruments WHERE id=p_instrument_id FOR UPDATE;
    IF p_currency IS NULL OR UPPER(TRIM(p_currency))<>currency_value THEN RAISE_APPLICATION_ERROR(-20700,'Quote currency differs from instrument'); END IF;
    source_value:=TRIM(p_source);
    UPDATE asset_prices SET price=p_price,currency=currency_value
      WHERE instrument_id=p_instrument_id AND SYS_EXTRACT_UTC(price_time_utc)=SYS_EXTRACT_UTC(p_price_time) AND source=source_value;
    IF SQL%ROWCOUNT=0 THEN
      INSERT INTO asset_prices(instrument_id,price_time_utc,price,currency,source)
        VALUES(p_instrument_id,p_price_time,p_price,currency_value,source_value);
    END IF;
    SELECT price,price_time_utc,source INTO latest_price,latest_time,latest_source FROM asset_prices
      WHERE instrument_id=p_instrument_id ORDER BY price_time_utc DESC,NLSSORT(source,'NLS_SORT=BINARY'),id FETCH FIRST 1 ROW ONLY;
    UPDATE quote_cache SET price=latest_price,currency=currency_value,timestamp=latest_time,source=latest_source WHERE instrument_id=p_instrument_id;
    IF SQL%ROWCOUNT=0 THEN
      INSERT INTO quote_cache(instrument_id,price,currency,timestamp,source)
        VALUES(p_instrument_id,latest_price,currency_value,latest_time,latest_source);
    END IF;
    audit_event(user_value,'MarketPriceSaved',p_instrument_id,source_value);
  EXCEPTION
    WHEN NO_DATA_FOUND THEN ROLLBACK TO market_api; RAISE_APPLICATION_ERROR(-20701,'Instrument unavailable');
    WHEN OTHERS THEN ROLLBACK TO market_api; RAISE;
  END;
  PROCEDURE put_exchange_rate(p_token VARCHAR2,p_rate_date DATE,p_from_currency VARCHAR2,
    p_to_currency VARCHAR2,p_rate NUMBER,p_source VARCHAR2) IS
    user_value RAW(16); from_value VARCHAR2(3); to_value VARCHAR2(3); lock_value VARCHAR2(3); source_value VARCHAR2(255);
  BEGIN
    SAVEPOINT market_api; user_value:=pkg_auth.require_admin(p_token); positive_number(p_rate);
    IF p_rate_date IS NULL OR TRUNC(p_rate_date)>TRUNC(CAST(SYS_EXTRACT_UTC(SYSTIMESTAMP) AS DATE))
      OR TRIM(p_from_currency) IS NULL OR LENGTH(TRIM(p_from_currency))<>3
      OR TRIM(p_to_currency) IS NULL OR LENGTH(TRIM(p_to_currency))<>3
      OR TRIM(p_source) IS NULL OR LENGTH(TRIM(p_source))>255 THEN RAISE_APPLICATION_ERROR(-20700,'Invalid exchange rate parameters'); END IF;
    from_value:=UPPER(TRIM(p_from_currency)); to_value:=UPPER(TRIM(p_to_currency)); source_value:=TRIM(p_source);
    currency_exists(from_value); currency_exists(to_value);
    IF from_value=to_value AND p_rate<>1 THEN RAISE_APPLICATION_ERROR(-20700,'Identity rate must equal one'); END IF;
    SELECT code INTO lock_value FROM currencies WHERE code=from_value FOR UPDATE;
    UPDATE exchange_rates SET rate=p_rate WHERE rate_date=TRUNC(p_rate_date) AND from_currency=from_value AND to_currency=to_value AND source=source_value;
    IF SQL%ROWCOUNT=0 THEN
      INSERT INTO exchange_rates(rate_date,from_currency,to_currency,rate,source) VALUES(TRUNC(p_rate_date),from_value,to_value,p_rate,source_value);
    END IF;
    audit_event(user_value,'ExchangeRateSaved',NULL,source_value);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO market_api; RAISE;
  END;
  PROCEDURE get_price_history(p_token VARCHAR2,p_instrument_id RAW,p_from_time TIMESTAMP WITH TIME ZONE,
    p_to_time TIMESTAMP WITH TIME ZONE,p_result OUT SYS_REFCURSOR) IS user_value RAW(16);
  BEGIN
    user_value:=pkg_auth.require_user(p_token);
    IF p_from_time IS NULL OR p_to_time IS NULL OR p_from_time>p_to_time THEN RAISE_APPLICATION_ERROR(-20700,'Invalid history interval'); END IF;
    OPEN p_result FOR SELECT RAWTOHEX(instrument_id) AS instrument_id,price_time_utc,price,currency,source FROM asset_prices
      WHERE instrument_id=p_instrument_id AND price_time_utc BETWEEN p_from_time AND p_to_time
      ORDER BY price_time_utc,NLSSORT(source,'NLS_SORT=BINARY'),id;
  END;
END pkg_market;
/
