WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_analytics AUTHID DEFINER AS
  PROCEDURE snapshot_as_of(p_token VARCHAR2,p_portfolio_id RAW,p_as_of TIMESTAMP WITH TIME ZONE,p_result OUT SYS_REFCURSOR);
  PROCEDURE instrument_risk(p_token VARCHAR2,p_instrument_id RAW,p_from_date DATE,p_to_date DATE,
    p_source VARCHAR2,p_periods_per_year NUMBER,p_risk_free_percent NUMBER,p_result OUT SYS_REFCURSOR);
END pkg_analytics;
/
CREATE OR REPLACE PACKAGE BODY pkg_analytics AS
  FUNCTION pair_rate(p_from VARCHAR2,p_to VARCHAR2,p_day DATE,p_detail OUT VARCHAR2) RETURN NUMBER IS
    rate_value NUMBER; date_value DATE; source_value VARCHAR2(255); direction_value VARCHAR2(16);
  BEGIN
    IF p_from=p_to THEN p_detail:='{"kind":"identity","rate":1}'; RETURN 1; END IF;
    SELECT converted_rate,rate_date,source,direction INTO rate_value,date_value,source_value,direction_value FROM (
      SELECT rate AS converted_rate,rate_date,source,'direct' AS direction,0 AS priority,id
        FROM exchange_rates WHERE from_currency=p_from AND to_currency=p_to AND rate_date<=TRUNC(p_day) AND rate>0
      UNION ALL
      SELECT 1/rate,rate_date,source,'inverse',1,id
        FROM exchange_rates WHERE from_currency=p_to AND to_currency=p_from AND rate_date<=TRUNC(p_day) AND rate>0
    ) ORDER BY rate_date DESC,priority,NLSSORT(source,'NLS_SORT=BINARY'),id FETCH FIRST 1 ROW ONLY;
    SELECT JSON_OBJECT('kind' VALUE direction_value,'from' VALUE p_from,'to' VALUE p_to,
      'rate' VALUE rate_value,'date' VALUE TO_CHAR(date_value,'YYYY-MM-DD'),'source' VALUE source_value)
      INTO p_detail FROM dual;
    RETURN rate_value;
  EXCEPTION WHEN NO_DATA_FOUND THEN p_detail:=NULL; RETURN NULL;
  END;
  FUNCTION fx_rate(p_from VARCHAR2,p_to VARCHAR2,p_day DATE,p_detail OUT VARCHAR2) RETURN NUMBER IS
    result NUMBER; first_rate NUMBER; second_rate NUMBER; first_detail VARCHAR2(4000); second_detail VARCHAR2(4000);
  BEGIN
    result:=pair_rate(p_from,p_to,p_day,p_detail);
    IF result IS NOT NULL OR p_from='BYN' OR p_to='BYN' THEN RETURN result; END IF;
    first_rate:=pair_rate(p_from,'BYN',p_day,first_detail); second_rate:=pair_rate('BYN',p_to,p_day,second_detail);
    IF first_rate IS NULL OR second_rate IS NULL THEN p_detail:=NULL; RETURN NULL; END IF;
    SELECT JSON_OBJECT('kind' VALUE 'BYN_cross','first' VALUE first_detail FORMAT JSON,
      'second' VALUE second_detail FORMAT JSON) INTO p_detail FROM dual;
    RETURN first_rate*second_rate;
  END;
  PROCEDURE snapshot_as_of(p_token VARCHAR2,p_portfolio_id RAW,p_as_of TIMESTAMP WITH TIME ZONE,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16); role_value VARCHAR2(255); base_value VARCHAR2(3); day_value DATE;
    cash_value NUMBER:=0; asset_value NUMBER:=0; flow_value NUMBER:=0; total_value NUMBER; pnl_value NUMBER; roi_value NUMBER;
    rate_value NUMBER; price_value NUMBER; price_time TIMESTAMP WITH TIME ZONE; price_source VARCHAR2(255);
    price_currency VARCHAR2(3); fx_detail VARCHAR2(4000); valuation_missing NUMBER:=0; flow_missing NUMBER:=0; n NUMBER;
    issues JSON_ARRAY_T:=JSON_ARRAY_T(); details JSON_ARRAY_T:=JSON_ARRAY_T(); item JSON_OBJECT_T;
    issues_value CLOB; details_value CLOB; missing_value NUMBER;
    PROCEDURE missing(p_kind VARCHAR2,p_id RAW,p_currency VARCHAR2) IS
      issue JSON_OBJECT_T:=JSON_OBJECT_T();
    BEGIN
      issue.put('kind',p_kind); issue.put('resource_id',RAWTOHEX(p_id)); issue.put('currency',p_currency); issues.append(issue);
    END;
    PROCEDURE detail(p_kind VARCHAR2,p_id RAW,p_amount NUMBER,p_currency VARCHAR2,p_fx VARCHAR2) IS
    BEGIN
      item:=JSON_OBJECT_T(); item.put('kind',p_kind); item.put('resource_id',RAWTOHEX(p_id));
      item.put('amount',p_amount); item.put('currency',p_currency); item.put('fx',JSON_OBJECT_T.parse(p_fx));
      details.append(item);
    END;
  BEGIN
    user_value:=pkg_auth.require_user(p_token);
    SELECT role INTO role_value FROM users WHERE id=user_value;
    IF role_value NOT IN ('PrivateInvestor','FinancialConsultant') THEN RAISE_APPLICATION_ERROR(-20720,'Investor or consultant required'); END IF;
    IF p_as_of IS NULL OR p_as_of>SYSTIMESTAMP THEN RAISE_APPLICATION_ERROR(-20721,'Nonfuture valuation timestamp required'); END IF;
    BEGIN
      SELECT base_currency INTO base_value FROM portfolios WHERE id=p_portfolio_id AND owner_user_id=user_value AND is_archived=0;
    EXCEPTION WHEN NO_DATA_FOUND THEN RAISE_APPLICATION_ERROR(-20722,'Portfolio unavailable'); END;
    day_value:=TRUNC(CAST(SYS_EXTRACT_UTC(p_as_of) AS DATE));
    SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p_portfolio_id AND trade_date<=p_as_of
      AND (type NOT IN ('Deposit','Withdrawal','Buy','Sell') OR type IS NULL);
    IF n<>0 THEN RAISE_APPLICATION_ERROR(-20721,'Unsupported transaction types in valuation history'); END IF;
    FOR r IN (SELECT currency,SUM(CASE WHEN type IN ('Deposit','Sell') THEN gross_amount ELSE -gross_amount END-fee_amount-tax_amount) AS balance
      FROM transactions WHERE portfolio_id=p_portfolio_id AND trade_date<=p_as_of GROUP BY currency) LOOP
      IF r.balance<>0 THEN
        rate_value:=fx_rate(r.currency,base_value,day_value,fx_detail);
        IF rate_value IS NULL THEN missing('cash_fx',NULL,r.currency); valuation_missing:=valuation_missing+1;
        ELSE cash_value:=cash_value+r.balance*rate_value; detail('cash',NULL,r.balance,r.currency,fx_detail); END IF;
      END IF;
    END LOOP;
    FOR r IN (SELECT a.id,a.instrument_id,SUM(CASE WHEN t.type='Buy' THEN t.quantity WHEN t.type='Sell' THEN -t.quantity ELSE 0 END) AS quantity
      FROM assets a JOIN transactions t ON t.asset_id=a.id AND t.portfolio_id=a.portfolio_id
      WHERE a.portfolio_id=p_portfolio_id AND t.trade_date<=p_as_of GROUP BY a.id,a.instrument_id) LOOP
      IF r.quantity<0 THEN RAISE_APPLICATION_ERROR(-20721,'Negative historical position'); END IF;
      IF r.quantity>0 THEN
        BEGIN
          SELECT price,currency,price_time_utc,source INTO price_value,price_currency,price_time,price_source FROM asset_prices
            WHERE instrument_id=r.instrument_id AND price_time_utc<=p_as_of AND price>0
            ORDER BY price_time_utc DESC,NLSSORT(source,'NLS_SORT=BINARY'),id FETCH FIRST 1 ROW ONLY;
        EXCEPTION WHEN NO_DATA_FOUND THEN price_value:=NULL; END;
        IF price_value IS NULL THEN missing('price',r.instrument_id,NULL); valuation_missing:=valuation_missing+1;
        ELSE
          rate_value:=fx_rate(price_currency,base_value,day_value,fx_detail);
          IF rate_value IS NULL THEN missing('asset_fx',r.instrument_id,price_currency); valuation_missing:=valuation_missing+1;
          ELSE
            asset_value:=asset_value+r.quantity*price_value*rate_value;
            detail('asset',r.instrument_id,r.quantity*price_value,price_currency,fx_detail);
            item.put('quantity',r.quantity); item.put('price',price_value); item.put('price_source',price_source);
            item.put('price_time',TO_CHAR(SYS_EXTRACT_UTC(price_time),'YYYY-MM-DD"T"HH24:MI:SS.FF6"Z"'));
            details.put(details.get_size-1,item,TRUE);
          END IF;
        END IF;
      END IF;
    END LOOP;
    FOR r IN (SELECT id,type,gross_amount,currency,trade_date FROM transactions
      WHERE portfolio_id=p_portfolio_id AND trade_date<=p_as_of AND type IN ('Deposit','Withdrawal')) LOOP
      rate_value:=fx_rate(r.currency,base_value,TRUNC(CAST(SYS_EXTRACT_UTC(r.trade_date) AS DATE)),fx_detail);
      IF rate_value IS NULL THEN missing('external_flow_fx',r.id,r.currency); flow_missing:=flow_missing+1;
      ELSE
        flow_value:=flow_value+CASE WHEN r.type='Deposit' THEN 1 ELSE -1 END*r.gross_amount*rate_value;
        detail('external_flow',r.id,CASE WHEN r.type='Deposit' THEN 1 ELSE -1 END*r.gross_amount,r.currency,fx_detail);
      END IF;
    END LOOP;
    missing_value:=issues.get_size;
    IF valuation_missing=0 THEN total_value:=cash_value+asset_value; ELSE cash_value:=NULL; asset_value:=NULL; END IF;
    IF flow_missing=0 THEN
      pnl_value:=total_value-flow_value;
      IF flow_value>0 THEN roi_value:=100*pnl_value/flow_value; END IF;
    ELSE flow_value:=NULL; END IF;
    issues_value:=issues.to_clob; details_value:=details.to_clob;
    OPEN p_result FOR SELECT base_value AS base_currency,p_as_of AS as_of_time,total_value AS total_value,
      cash_value AS cash_value,asset_value AS asset_value,flow_value AS net_external_flows,
      pnl_value AS pnl,roi_value AS roi_percent,missing_value AS missing_count,issues_value AS issues_json,
      details_value AS valuation_json FROM dual;
  END;
  PROCEDURE instrument_risk(p_token VARCHAR2,p_instrument_id RAW,p_from_date DATE,p_to_date DATE,
    p_source VARCHAR2,p_periods_per_year NUMBER,p_risk_free_percent NUMBER,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16); daily_rf NUMBER; configured_rf NUMBER; configured_currency VARCHAR2(3);
    instrument_currency VARCHAR2(3); risk_status VARCHAR2(64);
  BEGIN
    user_value:=pkg_auth.require_user(p_token);
    IF p_from_date IS NULL OR p_to_date IS NULL OR TRUNC(p_from_date)>TRUNC(p_to_date)
      OR TRIM(p_source) IS NULL OR LENGTH(TRIM(p_source))>255 OR p_periods_per_year IS NULL
      OR p_periods_per_year NOT IN (252,365) OR p_risk_free_percent<=-100
      OR p_risk_free_percent>=POWER(10,6) THEN RAISE_APPLICATION_ERROR(-20721,'Invalid explicit risk assumptions'); END IF;
    BEGIN
      SELECT s.risk_free_rate_percent,s.risk_free_currency,i.currency
        INTO configured_rf,configured_currency,instrument_currency FROM instruments i
        LEFT JOIN user_settings s ON s.owner_user_id=user_value WHERE i.id=p_instrument_id;
    EXCEPTION WHEN NO_DATA_FOUND THEN RAISE_APPLICATION_ERROR(-20701,'Instrument unavailable'); END;
    IF p_risk_free_percent IS NOT NULL AND (configured_rf IS NULL OR p_risk_free_percent<>configured_rf) THEN
      RAISE_APPLICATION_ERROR(-20721,'Risk-free rate must match saved settings');
    END IF;
    IF configured_rf IS NULL OR configured_currency IS NULL THEN risk_status:='MISSING_RISK_FREE_RATE';
    ELSIF configured_currency<>instrument_currency THEN risk_status:='RISK_FREE_CURRENCY_MISMATCH';
    ELSE daily_rf:=POWER(1+configured_rf/100,1/p_periods_per_year)-1; risk_status:='CONFIGURED'; END IF;
    OPEN p_result FOR WITH chosen AS (
      SELECT price,TRUNC(CAST(SYS_EXTRACT_UTC(price_time_utc) AS DATE)) AS price_day,
        ROW_NUMBER() OVER (PARTITION BY TRUNC(CAST(SYS_EXTRACT_UTC(price_time_utc) AS DATE)) ORDER BY price_time_utc DESC,id) AS rn
      FROM asset_prices WHERE instrument_id=p_instrument_id AND source=TRIM(p_source) AND price>0
        AND SYS_EXTRACT_UTC(price_time_utc)>=CAST(TRUNC(p_from_date) AS TIMESTAMP)
        AND SYS_EXTRACT_UTC(price_time_utc)<CAST(TRUNC(p_to_date)+1 AS TIMESTAMP)
        AND SYS_EXTRACT_UTC(price_time_utc)<CAST(TRUNC(CAST(SYS_EXTRACT_UTC(SYSTIMESTAMP) AS DATE)) AS TIMESTAMP)
    ), series AS (
      SELECT price_day,price,LAG(price) OVER (ORDER BY price_day) AS previous_price,
        price_day-LAG(price_day) OVER (ORDER BY price_day) AS gap_days,
        MAX(price) OVER (ORDER BY price_day ROWS UNBOUNDED PRECEDING) AS running_peak FROM chosen WHERE rn=1
    ), stats AS (
      SELECT COUNT(*) AS price_count,COUNT(previous_price) AS return_count,AVG(price/previous_price-1) AS average_return,
        STDDEV_SAMP(price/previous_price-1) AS sample_sigma,MAX(1-price/running_peak) AS drawdown,
        MAX(gap_days) AS max_gap,MIN(price_day) AS first_day,MAX(price_day) AS last_day FROM series
    ) SELECT price_count,return_count,first_day,last_day,TRIM(p_source) AS source,
      p_periods_per_year AS periods_per_year,configured_rf AS risk_free_rate_percent,
      CASE WHEN return_count>=2 THEN sample_sigma*SQRT(p_periods_per_year) END AS annualized_volatility,
      CASE WHEN return_count>=2 AND sample_sigma>0 AND daily_rf IS NOT NULL THEN (average_return-daily_rf)/sample_sigma*SQRT(p_periods_per_year) END AS sharpe_ratio,
      CASE WHEN return_count>=2 THEN drawdown END AS maximum_drawdown,
      max_gap AS max_calendar_gap_days,
      CASE WHEN return_count<2 THEN 'INSUFFICIENT_DATA' ELSE 'OBSERVED_DAILY_SERIES' END AS calculation_status,
      risk_status AS risk_free_status FROM stats;
  END;
END pkg_analytics;
/
