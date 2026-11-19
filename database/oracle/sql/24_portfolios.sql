WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_portfolios AUTHID DEFINER AS
  PROCEDURE list_portfolios(p_token VARCHAR2,p_result OUT SYS_REFCURSOR);
  PROCEDURE create_portfolio(p_token VARCHAR2,p_name VARCHAR2,p_base_currency VARCHAR2,p_description VARCHAR2,p_client_label VARCHAR2,p_id OUT RAW);
  PROCEDURE update_portfolio(p_token VARCHAR2,p_id RAW,p_name VARCHAR2,p_description VARCHAR2,p_client_label VARCHAR2);
  PROCEDURE archive_portfolio(p_token VARCHAR2,p_id RAW);
  PROCEDURE list_assets(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR);
  PROCEDURE list_transactions(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR);
  PROCEDURE post_cash(p_token VARCHAR2,p_portfolio_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_amount NUMBER,p_currency VARCHAR2,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0);
  PROCEDURE post_trade(p_token VARCHAR2,p_portfolio_id RAW,p_instrument_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0);
  PROCEDURE update_transaction(p_token VARCHAR2,p_transaction_id RAW,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_gross_amount NUMBER,p_fee_amount NUMBER,p_operation_order NUMBER DEFAULT NULL);
  PROCEDURE remove_transaction(p_token VARCHAR2,p_transaction_id RAW);
  PROCEDURE get_balances(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR);
  PROCEDURE get_realized_pnl(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR);
END pkg_portfolios;
/
CREATE OR REPLACE PACKAGE BODY pkg_portfolios AS
  FUNCTION actor(p_token VARCHAR2) RETURN RAW IS
    result RAW(16); role_value VARCHAR2(255);
  BEGIN
    pkg_auth.begin_request(p_token); result:=pkg_auth.current_user_id;
    SELECT role INTO role_value FROM users WHERE id=result;
    IF role_value NOT IN ('PrivateInvestor','FinancialConsultant') THEN RAISE_APPLICATION_ERROR(-20400,'Investor or consultant role required'); END IF;
    RETURN result;
  END;
  PROCEDURE owned(p_user RAW,p_id RAW,p_lock BOOLEAN DEFAULT FALSE) IS
    owner_value RAW(16); archived NUMBER;
  BEGIN
    IF p_lock THEN
      SELECT owner_user_id,is_archived INTO owner_value,archived FROM portfolios WHERE id=p_id FOR UPDATE;
    ELSE
      SELECT owner_user_id,is_archived INTO owner_value,archived FROM portfolios WHERE id=p_id;
    END IF;
    IF owner_value<>p_user OR archived<>0 THEN RAISE_APPLICATION_ERROR(-20401,'Portfolio unavailable'); END IF;
  EXCEPTION WHEN NO_DATA_FOUND THEN RAISE_APPLICATION_ERROR(-20401,'Portfolio unavailable');
  END;
  PROCEDURE valid_label(p_user RAW,p_label VARCHAR2) IS
    role_value VARCHAR2(255);
  BEGIN
    SELECT role INTO role_value FROM users WHERE id=p_user;
    IF TRIM(p_label) IS NOT NULL AND role_value<>'FinancialConsultant' THEN RAISE_APPLICATION_ERROR(-20402,'Client label requires consultant role'); END IF;
  END;
  FUNCTION details(p_id RAW) RETURN CLOB IS
    result CLOB;
  BEGIN
    SELECT JSON_OBJECT('name' VALUE name,'description' VALUE description,'client_label' VALUE client_label,
      'base_currency' VALUE base_currency,'is_archived' VALUE is_archived RETURNING CLOB)
      INTO result FROM portfolios WHERE id=p_id;
    RETURN result;
  END;
  PROCEDURE audit_change(p_user RAW,p_action VARCHAR2,p_id RAW,p_before CLOB,p_after CLOB) IS
  BEGIN
    INSERT INTO audit_log(user_id,action,timestamp,metadata_json)
      VALUES(p_user,p_action,SYSTIMESTAMP,JSON_OBJECT('portfolio_id' VALUE RAWTOHEX(p_id),
        'before' VALUE p_before FORMAT JSON,'after' VALUE p_after FORMAT JSON RETURNING CLOB));
  END;
  PROCEDURE list_portfolios(p_token VARCHAR2,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    OPEN p_result FOR SELECT RAWTOHEX(id) AS portfolio_id,name,description,client_label,base_currency,is_archived,created_at,updated_at
      FROM portfolios WHERE owner_user_id=user_value AND is_archived=0 ORDER BY created_at,id;
  END;
  PROCEDURE create_portfolio(p_token VARCHAR2,p_name VARCHAR2,p_base_currency VARCHAR2,p_description VARCHAR2,p_client_label VARCHAR2,p_id OUT RAW) IS
    user_value RAW(16);
  BEGIN
    SAVEPOINT portfolio_api; user_value:=actor(p_token); valid_label(user_value,p_client_label);
    pkg_ledger.create_portfolio(user_value,p_name,p_base_currency,p_id);
    UPDATE portfolios SET description=p_description,client_label=TRIM(p_client_label) WHERE id=p_id;
    audit_change(user_value,'PortfolioDetailsCreated',p_id,NULL,details(p_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO portfolio_api; p_id:=NULL; RAISE;
  END;
  PROCEDURE update_portfolio(p_token VARCHAR2,p_id RAW,p_name VARCHAR2,p_description VARCHAR2,p_client_label VARCHAR2) IS
    user_value RAW(16); before_value CLOB;
  BEGIN
    SAVEPOINT portfolio_api; user_value:=actor(p_token); owned(user_value,p_id,TRUE); valid_label(user_value,p_client_label);
    IF TRIM(p_name) IS NULL THEN RAISE_APPLICATION_ERROR(-20403,'Portfolio name required'); END IF;
    before_value:=details(p_id);
    UPDATE portfolios SET name=TRIM(p_name),description=p_description,client_label=TRIM(p_client_label),updated_at=SYSTIMESTAMP WHERE id=p_id;
    audit_change(user_value,'PortfolioUpdated',p_id,before_value,details(p_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO portfolio_api; RAISE;
  END;
  PROCEDURE archive_portfolio(p_token VARCHAR2,p_id RAW) IS
    user_value RAW(16); before_value CLOB;
  BEGIN
    SAVEPOINT portfolio_api; user_value:=actor(p_token); owned(user_value,p_id,TRUE); before_value:=details(p_id);
    UPDATE portfolios SET is_archived=1,updated_at=SYSTIMESTAMP WHERE id=p_id;
    audit_change(user_value,'PortfolioArchived',p_id,before_value,details(p_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO portfolio_api; RAISE;
  END;
  PROCEDURE list_assets(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token); owned(user_value,p_portfolio_id);
    OPEN p_result FOR SELECT RAWTOHEX(a.id) AS asset_id,RAWTOHEX(a.instrument_id) AS instrument_id,
      i.ticker,i.name,i.currency,a.quantity,a.average_buy_price,a.current_price,a.is_archived
      FROM assets a JOIN instruments i ON i.id=a.instrument_id WHERE a.portfolio_id=p_portfolio_id ORDER BY i.ticker,a.id;
  END;
  PROCEDURE list_transactions(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token); owned(user_value,p_portfolio_id);
    OPEN p_result FOR SELECT RAWTOHEX(id) AS transaction_id,RAWTOHEX(asset_id) AS asset_id,type,trade_date,
      quantity,price,gross_amount,currency,fee_amount,tax_amount,broker,external_id,operation_order
      FROM transactions WHERE portfolio_id=p_portfolio_id ORDER BY trade_date,operation_order,id;
  END;
  PROCEDURE post_cash(p_token VARCHAR2,p_portfolio_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_amount NUMBER,p_currency VARCHAR2,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0) IS
    user_value RAW(16);
  BEGIN
    SAVEPOINT portfolio_api; user_value:=actor(p_token); owned(user_value,p_portfolio_id);
    pkg_ledger.post_cash(user_value,p_portfolio_id,p_type,p_trade_date,p_amount,p_currency,p_fee_amount,p_id,p_operation_order);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO portfolio_api; p_id:=NULL; RAISE;
  END;
  PROCEDURE post_trade(p_token VARCHAR2,p_portfolio_id RAW,p_instrument_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0) IS
    user_value RAW(16);
  BEGIN
    SAVEPOINT portfolio_api; user_value:=actor(p_token); owned(user_value,p_portfolio_id);
    pkg_ledger.post_trade(user_value,p_portfolio_id,p_instrument_id,p_type,p_trade_date,p_quantity,p_price,p_fee_amount,p_id,p_operation_order);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO portfolio_api; p_id:=NULL; RAISE;
  END;
  PROCEDURE update_transaction(p_token VARCHAR2,p_transaction_id RAW,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_gross_amount NUMBER,p_fee_amount NUMBER,p_operation_order NUMBER DEFAULT NULL) IS
    user_value RAW(16); portfolio_value RAW(16);
  BEGIN
    SAVEPOINT portfolio_api; user_value:=actor(p_token);
    SELECT portfolio_id INTO portfolio_value FROM transactions WHERE id=p_transaction_id;
    owned(user_value,portfolio_value);
    pkg_ledger.update_transaction(user_value,p_transaction_id,p_trade_date,p_quantity,p_price,p_gross_amount,p_fee_amount,p_operation_order);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO portfolio_api; RAISE;
  END;
  PROCEDURE remove_transaction(p_token VARCHAR2,p_transaction_id RAW) IS
    user_value RAW(16); portfolio_value RAW(16);
  BEGIN
    SAVEPOINT portfolio_api; user_value:=actor(p_token);
    SELECT portfolio_id INTO portfolio_value FROM transactions WHERE id=p_transaction_id;
    owned(user_value,portfolio_value); pkg_ledger.remove_transaction(user_value,p_transaction_id);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO portfolio_api; RAISE;
  END;
  PROCEDURE get_balances(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token); owned(user_value,p_portfolio_id);
    pkg_ledger.get_balances(user_value,p_portfolio_id,p_result);
  END;
  PROCEDURE get_realized_pnl(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token); owned(user_value,p_portfolio_id);
    pkg_ledger.get_realized_pnl(user_value,p_portfolio_id,p_result);
  END;
END pkg_portfolios;
/
