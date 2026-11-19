WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_ledger AUTHID DEFINER AS
  PROCEDURE create_portfolio(p_owner_user_id RAW,p_name VARCHAR2,p_base_currency VARCHAR2,p_id OUT RAW);
  PROCEDURE post_cash(p_owner_user_id RAW,p_portfolio_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_amount NUMBER,p_currency VARCHAR2,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0);
  PROCEDURE post_trade(p_owner_user_id RAW,p_portfolio_id RAW,p_instrument_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0);
  PROCEDURE update_transaction(p_owner_user_id RAW,p_transaction_id RAW,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_gross_amount NUMBER,p_fee_amount NUMBER,p_operation_order NUMBER DEFAULT NULL);
  PROCEDURE remove_transaction(p_owner_user_id RAW,p_transaction_id RAW);
  PROCEDURE get_balances(p_owner_user_id RAW,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR);
  PROCEDURE get_realized_pnl(p_owner_user_id RAW,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR);
  PROCEDURE recalculate(p_owner_user_id RAW,p_portfolio_id RAW);
END pkg_ledger;
/
CREATE OR REPLACE PACKAGE BODY pkg_ledger AS
  PROCEDURE trusted(p_owner RAW) IS
    n PLS_INTEGER;
  BEGIN
    IF SYS_CONTEXT('USERENV','SESSION_USER') NOT IN ('SYS','PROXIMA_OWNER') THEN
      IF pkg_auth.current_user_id<>p_owner THEN
        RAISE_APPLICATION_ERROR(-20100,'Authenticated ledger owner mismatch');
      END IF;
      SELECT COUNT(*) INTO n FROM users WHERE id=p_owner
        AND role IN ('PrivateInvestor','FinancialConsultant') AND account_status='Active';
      IF n<>1 THEN RAISE_APPLICATION_ERROR(-20100,'Portfolio user role required'); END IF;
    END IF;
    SELECT COUNT(*) INTO n FROM users WHERE id=p_owner AND account_status='Active';
    IF n<>1 THEN RAISE_APPLICATION_ERROR(-20101,'Owner must exist and have explicit Active status'); END IF;
  END;
  PROCEDURE portfolio_lock(p_owner RAW,p_portfolio RAW) IS
    owner_id RAW(16); archived NUMBER;
  BEGIN
    trusted(p_owner);
    SELECT owner_user_id,is_archived INTO owner_id,archived FROM portfolios WHERE id=p_portfolio FOR UPDATE;
    IF owner_id<>p_owner OR archived<>0 THEN RAISE_APPLICATION_ERROR(-20102,'Portfolio unavailable'); END IF;
  END;
  PROCEDURE log_change(p_owner RAW,p_action VARCHAR2,p_portfolio RAW,p_transaction RAW,p_before CLOB,p_after CLOB) IS
  BEGIN
    INSERT INTO audit_log(user_id,action,timestamp,metadata_json)
    VALUES(p_owner,p_action,SYSTIMESTAMP,JSON_OBJECT('portfolio_id' VALUE RAWTOHEX(p_portfolio),
      'transaction_id' VALUE RAWTOHEX(p_transaction),
      'database_session_user' VALUE SYS_CONTEXT('USERENV','SESSION_USER'),'before' VALUE p_before FORMAT JSON,
      'after' VALUE p_after FORMAT JSON RETURNING CLOB));
  END;
  FUNCTION transaction_json(p_id RAW) RETURN CLOB IS
    result CLOB;
  BEGIN
    SELECT JSON_OBJECT('id' VALUE RAWTOHEX(id),'type' VALUE type,
      'trade_date' VALUE TO_CHAR(trade_date,'YYYY-MM-DD"T"HH24:MI:SS.FF9TZH:TZM'),
      'asset_id' VALUE RAWTOHEX(asset_id),'currency' VALUE currency,'quantity' VALUE quantity,
      'price' VALUE price,'gross_amount' VALUE gross_amount,'fee_amount' VALUE fee_amount,
      'tax_amount' VALUE tax_amount,'operation_order' VALUE operation_order RETURNING CLOB)
    INTO result FROM transactions WHERE id=p_id;
    RETURN result;
  END;
  PROCEDURE valid_order(p_portfolio RAW,p_time TIMESTAMP WITH TIME ZONE,p_order NUMBER,p_exclude RAW DEFAULT NULL) IS
    n NUMBER;
  BEGIN
    IF p_order IS NULL OR p_order<0 OR p_order<>TRUNC(p_order) OR p_order>999999999999999999 THEN
      RAISE_APPLICATION_ERROR(-20103,'Operation order must be a nonnegative integer up to 18 digits');
    END IF;
    SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=p_portfolio AND trade_date=p_time
      AND operation_order=p_order AND (p_exclude IS NULL OR id<>p_exclude);
    IF n>0 THEN RAISE_APPLICATION_ERROR(-20103,'Duplicate operation timestamp and order'); END IF;
  END;
  PROCEDURE replay(p_portfolio RAW) IS
    TYPE numbers IS TABLE OF NUMBER INDEX BY VARCHAR2(32);
    cash numbers; quantities numbers; costs numbers;
    TYPE lot_record IS RECORD(asset_key VARCHAR2(32),remaining NUMBER,unit_cost NUMBER);
    TYPE lots_type IS TABLE OF lot_record INDEX BY PLS_INTEGER;
    lots lots_type; lot_count PLS_INTEGER:=0;
    key VARCHAR2(32); needed NUMBER; taken NUMBER; n PLS_INTEGER;
    prev_time TIMESTAMP WITH TIME ZONE; prev_order NUMBER; instrument_currency instruments.currency%TYPE;
  BEGIN
    FOR r IN (SELECT * FROM transactions WHERE portfolio_id=p_portfolio ORDER BY trade_date,operation_order,id) LOOP
      IF prev_time IS NOT NULL AND prev_time=r.trade_date AND prev_order=r.operation_order THEN
        RAISE_APPLICATION_ERROR(-20103,'Duplicate operation timestamp and order');
      END IF;
      prev_time:=r.trade_date; prev_order:=r.operation_order;
      IF r.operation_order IS NULL OR r.operation_order<0 OR r.operation_order<>TRUNC(r.operation_order)
        OR r.operation_order>999999999999999999 THEN RAISE_APPLICATION_ERROR(-20103,'Invalid operation order'); END IF;
      IF r.type IS NULL OR r.quantity IS NULL OR r.price IS NULL OR r.gross_amount IS NULL
        OR r.fee_amount IS NULL OR r.tax_amount IS NULL OR r.currency IS NULL OR r.trade_date IS NULL
        OR r.type NOT IN ('Deposit','Withdrawal','Buy','Sell') OR r.quantity<0 OR r.price<0
        OR r.gross_amount<=0 OR r.fee_amount<0 OR r.tax_amount<>0 THEN
        RAISE_APPLICATION_ERROR(-20104,'Unsupported operation or invalid nonnegative amounts');
      END IF;
      IF NOT cash.EXISTS(r.currency) THEN cash(r.currency):=0; END IF;
      IF r.type IN ('Deposit','Withdrawal') THEN
        IF r.asset_id IS NOT NULL OR r.quantity<>0 OR r.price<>0 THEN
          RAISE_APPLICATION_ERROR(-20105,'Cash operation must not carry an asset or quantity');
        END IF;
        IF r.type='Deposit' THEN cash(r.currency):=cash(r.currency)+r.gross_amount-r.fee_amount;
        ELSE cash(r.currency):=cash(r.currency)-r.gross_amount-r.fee_amount; END IF;
      ELSE
        IF r.asset_id IS NULL OR r.quantity<=0 OR r.price<=0 OR r.gross_amount<>ROUND(r.quantity*r.price,10) THEN
          RAISE_APPLICATION_ERROR(-20106,'Trade requires positive quantity/price and exact rounded gross');
        END IF;
        SELECT i.currency INTO instrument_currency FROM assets a JOIN instruments i ON i.id=a.instrument_id
          WHERE a.id=r.asset_id AND a.portfolio_id=p_portfolio;
        IF instrument_currency<>r.currency THEN RAISE_APPLICATION_ERROR(-20107,'Trade currency must match instrument'); END IF;
        key:=RAWTOHEX(r.asset_id);
        IF NOT quantities.EXISTS(key) THEN quantities(key):=0; costs(key):=0; END IF;
        IF r.type='Buy' THEN
          cash(r.currency):=cash(r.currency)-r.gross_amount-r.fee_amount;
          quantities(key):=quantities(key)+r.quantity; costs(key):=costs(key)+r.gross_amount+r.fee_amount;
          lot_count:=lot_count+1; lots(lot_count).asset_key:=key;
          lots(lot_count).remaining:=r.quantity; lots(lot_count).unit_cost:=(r.gross_amount+r.fee_amount)/r.quantity;
        ELSE
          IF quantities(key)<r.quantity THEN RAISE_APPLICATION_ERROR(-20108,'Negative historical instrument balance'); END IF;
          needed:=r.quantity;
          FOR j IN 1..lot_count LOOP
            IF lots(j).asset_key=key AND lots(j).remaining>0 AND needed>0 THEN
              taken:=LEAST(needed,lots(j).remaining);
              lots(j).remaining:=lots(j).remaining-taken; needed:=needed-taken;
              costs(key):=costs(key)-taken*lots(j).unit_cost;
            END IF;
          END LOOP;
          quantities(key):=quantities(key)-r.quantity;
          cash(r.currency):=cash(r.currency)+r.gross_amount-r.fee_amount;
        END IF;
      END IF;
      IF cash(r.currency)<0 THEN RAISE_APPLICATION_ERROR(-20109,'Negative historical cash balance'); END IF;
    END LOOP;
    UPDATE assets SET quantity=0,average_buy_price=NULL WHERE portfolio_id=p_portfolio;
    key:=quantities.FIRST;
    WHILE key IS NOT NULL LOOP
      UPDATE assets SET quantity=quantities(key),average_buy_price=
        CASE WHEN quantities(key)>0 THEN costs(key)/quantities(key) ELSE NULL END WHERE id=HEXTORAW(key);
      key:=quantities.NEXT(key);
    END LOOP;
    UPDATE portfolios SET updated_at=SYSTIMESTAMP WHERE id=p_portfolio;
  END;
  PROCEDURE create_portfolio(p_owner_user_id RAW,p_name VARCHAR2,p_base_currency VARCHAR2,p_id OUT RAW) IS
    after_value CLOB;
  BEGIN
    SAVEPOINT ledger_mutation;
    trusted(p_owner_user_id);
    IF TRIM(p_name) IS NULL THEN RAISE_APPLICATION_ERROR(-20110,'Portfolio name required'); END IF;
    p_id:=SYS_GUID();
    INSERT INTO portfolios(id,owner_user_id,name,base_currency) VALUES(p_id,p_owner_user_id,TRIM(p_name),p_base_currency);
    SELECT JSON_OBJECT('name' VALUE TRIM(p_name),'base_currency' VALUE p_base_currency RETURNING CLOB)
      INTO after_value FROM dual;
    log_change(p_owner_user_id,'PortfolioCreated',p_id,NULL,NULL,after_value);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO ledger_mutation; p_id:=NULL; RAISE;
  END;
  PROCEDURE post_cash(p_owner_user_id RAW,p_portfolio_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_amount NUMBER,p_currency VARCHAR2,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0) IS
  BEGIN
    SAVEPOINT ledger_mutation;
    portfolio_lock(p_owner_user_id,p_portfolio_id);
    valid_order(p_portfolio_id,p_trade_date,p_operation_order);
    IF p_type IS NULL OR p_type NOT IN ('Deposit','Withdrawal') THEN RAISE_APPLICATION_ERROR(-20111,'Expected Deposit or Withdrawal'); END IF;
    p_id:=SYS_GUID();
    INSERT INTO transactions(id,portfolio_id,type,trade_date,quantity,price,gross_amount,currency,fee_amount,tax_amount,operation_order)
      VALUES(p_id,p_portfolio_id,p_type,p_trade_date,0,0,p_amount,p_currency,p_fee_amount,0,p_operation_order);
    replay(p_portfolio_id);
    log_change(p_owner_user_id,'CashPosted',p_portfolio_id,p_id,NULL,transaction_json(p_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO ledger_mutation; p_id:=NULL; RAISE;
  END;
  PROCEDURE post_trade(p_owner_user_id RAW,p_portfolio_id RAW,p_instrument_id RAW,p_type VARCHAR2,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_fee_amount NUMBER,p_id OUT RAW,p_operation_order NUMBER DEFAULT 0) IS
    asset_id_value RAW(16); currency_value instruments.currency%TYPE;
  BEGIN
    SAVEPOINT ledger_mutation;
    portfolio_lock(p_owner_user_id,p_portfolio_id);
    valid_order(p_portfolio_id,p_trade_date,p_operation_order);
    IF p_type IS NULL OR p_type NOT IN ('Buy','Sell') THEN RAISE_APPLICATION_ERROR(-20112,'Expected Buy or Sell'); END IF;
    SELECT currency INTO currency_value FROM instruments WHERE id=p_instrument_id;
    BEGIN
      SELECT id INTO asset_id_value FROM assets WHERE portfolio_id=p_portfolio_id AND instrument_id=p_instrument_id;
    EXCEPTION WHEN NO_DATA_FOUND THEN
      asset_id_value:=SYS_GUID();
      INSERT INTO assets(id,portfolio_id,instrument_id,quantity) VALUES(asset_id_value,p_portfolio_id,p_instrument_id,0);
    END;
    p_id:=SYS_GUID();
    INSERT INTO transactions(id,portfolio_id,asset_id,type,trade_date,quantity,price,gross_amount,currency,fee_amount,tax_amount,operation_order)
      VALUES(p_id,p_portfolio_id,asset_id_value,p_type,p_trade_date,ROUND(p_quantity,10),ROUND(p_price,10),ROUND(ROUND(p_quantity,10)*ROUND(p_price,10),10),currency_value,p_fee_amount,0,p_operation_order);
    replay(p_portfolio_id);
    log_change(p_owner_user_id,'TradePosted',p_portfolio_id,p_id,NULL,transaction_json(p_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO ledger_mutation; p_id:=NULL; RAISE;
  END;
  PROCEDURE update_transaction(p_owner_user_id RAW,p_transaction_id RAW,p_trade_date TIMESTAMP WITH TIME ZONE,p_quantity NUMBER,p_price NUMBER,p_gross_amount NUMBER,p_fee_amount NUMBER,p_operation_order NUMBER DEFAULT NULL) IS
    portfolio_id_value RAW(16); before_value CLOB; order_value NUMBER;
  BEGIN
    SAVEPOINT ledger_mutation;
    trusted(p_owner_user_id);
    SELECT portfolio_id INTO portfolio_id_value FROM transactions WHERE id=p_transaction_id;
    portfolio_lock(p_owner_user_id,portfolio_id_value);
    SELECT NVL(p_operation_order,operation_order) INTO order_value FROM transactions WHERE id=p_transaction_id;
    valid_order(portfolio_id_value,p_trade_date,order_value,p_transaction_id);
    before_value:=transaction_json(p_transaction_id);
    UPDATE transactions SET operation_order=order_value,trade_date=p_trade_date,quantity=p_quantity,price=p_price,gross_amount=p_gross_amount,fee_amount=p_fee_amount WHERE id=p_transaction_id;
    IF SQL%ROWCOUNT<>1 THEN RAISE_APPLICATION_ERROR(-20113,'Operation no longer exists'); END IF;
    replay(portfolio_id_value);
    log_change(p_owner_user_id,'TransactionUpdated',portfolio_id_value,p_transaction_id,before_value,transaction_json(p_transaction_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO ledger_mutation; RAISE;
  END;
  PROCEDURE remove_transaction(p_owner_user_id RAW,p_transaction_id RAW) IS
    portfolio_id_value RAW(16); before_value CLOB;
  BEGIN
    SAVEPOINT ledger_mutation;
    trusted(p_owner_user_id);
    SELECT portfolio_id INTO portfolio_id_value FROM transactions WHERE id=p_transaction_id;
    portfolio_lock(p_owner_user_id,portfolio_id_value);
    before_value:=transaction_json(p_transaction_id);
    DELETE FROM transactions WHERE id=p_transaction_id;
    IF SQL%ROWCOUNT<>1 THEN RAISE_APPLICATION_ERROR(-20113,'Operation no longer exists'); END IF;
    replay(portfolio_id_value);
    log_change(p_owner_user_id,'TransactionRemoved',portfolio_id_value,p_transaction_id,before_value,NULL);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO ledger_mutation; RAISE;
  END;
  PROCEDURE get_balances(p_owner_user_id RAW,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR) IS
  BEGIN
    portfolio_lock(p_owner_user_id,p_portfolio_id);
    OPEN p_result FOR SELECT currency,SUM(CASE WHEN type IN ('Deposit','Sell') THEN gross_amount ELSE -gross_amount END-fee_amount) AS cash_balance
      FROM transactions WHERE portfolio_id=p_portfolio_id GROUP BY currency ORDER BY currency;
  END;
  PROCEDURE get_realized_pnl(p_owner_user_id RAW,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR) IS
  BEGIN
    portfolio_lock(p_owner_user_id,p_portfolio_id);
    OPEN p_result FOR
      WITH b AS (SELECT asset_id,quantity,(gross_amount+fee_amount)/quantity AS unit_cost,
          SUM(quantity) OVER(PARTITION BY asset_id ORDER BY trade_date,operation_order,id ROWS UNBOUNDED PRECEDING) AS q_end
        FROM transactions WHERE portfolio_id=p_portfolio_id AND type='Buy'),
      s AS (SELECT id,asset_id,currency,trade_date,operation_order,quantity,gross_amount,fee_amount,
          SUM(quantity) OVER(PARTITION BY asset_id ORDER BY trade_date,operation_order,id ROWS UNBOUNDED PRECEDING) AS q_end
        FROM transactions WHERE portfolio_id=p_portfolio_id AND type='Sell')
      SELECT RAWTOHEX(s.id) AS transaction_id,RAWTOHEX(s.asset_id) AS asset_id,s.currency,s.trade_date,
        s.gross_amount-s.fee_amount AS net_proceeds,
        SUM(GREATEST(0,LEAST(s.q_end,b.q_end)-GREATEST(s.q_end-s.quantity,b.q_end-b.quantity))*b.unit_cost) AS fifo_cost,
        s.gross_amount-s.fee_amount-SUM(GREATEST(0,LEAST(s.q_end,b.q_end)-GREATEST(s.q_end-s.quantity,b.q_end-b.quantity))*b.unit_cost) AS realized_pnl
      FROM s JOIN b ON b.asset_id=s.asset_id AND b.q_end>s.q_end-s.quantity AND b.q_end-b.quantity<s.q_end
      GROUP BY s.id,s.asset_id,s.currency,s.trade_date,s.operation_order,s.gross_amount,s.fee_amount ORDER BY s.trade_date,s.operation_order,s.id;
  END;
  PROCEDURE recalculate(p_owner_user_id RAW,p_portfolio_id RAW) IS
  BEGIN
    SAVEPOINT ledger_mutation;
    portfolio_lock(p_owner_user_id,p_portfolio_id);
    replay(p_portfolio_id);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO ledger_mutation; RAISE;
  END;
END pkg_ledger;
/
