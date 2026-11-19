WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
DECLARE
  u RAW(16):=SYS_GUID(); p RAW(16); deposit_id RAW(16); b1 RAW(16); b2 RAW(16); sale RAW(16); rejected RAW(16);
  instrument RAW(16):=HEXTORAW('00000000000000000000000000000001');
  moment TIMESTAMP WITH TIME ZONE:=TO_TIMESTAMP_TZ('2026-01-02 12:00:00 +00:00','YYYY-MM-DD HH24:MI:SS TZH:TZM');
  c SYS_REFCURSOR; tx VARCHAR2(32); asset VARCHAR2(32); currency_code VARCHAR2(3);
  trade_time TIMESTAMP WITH TIME ZONE; proceeds NUMBER; cost NUMBER; profit NUMBER;
  n NUMBER; value NUMBER; code NUMBER;
  PROCEDURE require(ok BOOLEAN,label VARCHAR2) IS
  BEGIN IF ok IS NULL OR NOT ok THEN RAISE_APPLICATION_ERROR(-20994,label); END IF; END;
  PROCEDURE unchanged IS
  BEGIN
    SELECT COUNT(*) INTO n FROM proxima_owner.transactions WHERE portfolio_id=p;
    require(n=4,'exact_four_ordered_operations');
    SELECT COUNT(*) INTO n FROM proxima_owner.audit_log WHERE user_id=u;
    require(n=5,'rejection_keeps_audit_atomic');
    SELECT quantity,average_buy_price INTO n,value FROM proxima_owner.assets WHERE portfolio_id=p;
    require(n=8 AND value=121,'remaining_quantity_and_fee_adjusted_basis');
    proxima_owner.pkg_ledger.get_balances(u,p,c); FETCH c INTO currency_code,value;
    require(c%FOUND AND currency_code='USD' AND value=9574,'ordered_cash_9574'); CLOSE c;
    proxima_owner.pkg_ledger.get_realized_pnl(u,p,c);
    FETCH c INTO tx,asset,currency_code,trade_time,proceeds,cost,profit;
    require(c%FOUND AND tx=RAWTOHEX(sale) AND proceeds=1794 AND cost=1252 AND profit=542,'ordered_fifo_1252_profit_542');
    FETCH c INTO tx,asset,currency_code,trade_time,proceeds,cost,profit;
    require(c%NOTFOUND,'one_realized_sale'); CLOSE c;
  END;
BEGIN
  SAVEPOINT ordered_fixture;
  INSERT INTO proxima_owner.users(id,login,display_name,role,password_hash,account_status)
    VALUES(u,'order-test-'||RAWTOHEX(u),'Rollback ordered fixture','PrivateInvestor','not-a-login-credential','Active');
  proxima_owner.pkg_ledger.create_portfolio(u,'Ordered fixture','USD',p);
  proxima_owner.pkg_ledger.post_cash(u,p,'Deposit',moment-INTERVAL '1' DAY,10000,'USD',0,deposit_id,0);
  proxima_owner.pkg_ledger.post_trade(u,p,instrument,'Buy',moment,10,100,10,b1,1);
  proxima_owner.pkg_ledger.post_trade(u,p,instrument,'Buy',moment,10,120,10,b2,2);
  proxima_owner.pkg_ledger.post_trade(u,p,instrument,'Sell',moment,12,150,6,sale,3);
  unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS ordered_same_instant_fifo_and_cash');
  code:=0;
  BEGIN
    proxima_owner.pkg_ledger.post_trade(u,p,instrument,'Buy',moment,1,100,0,rejected,2);
  EXCEPTION WHEN OTHERS THEN code:=SQLCODE; END;
  require(code=-20103,'duplicate_order_rejected'); unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS duplicate_same_instant_order_is_atomic');
  code:=0;
  BEGIN
    proxima_owner.pkg_ledger.update_transaction(u,b1,moment,10,100,1000,10,4);
  EXCEPTION WHEN OTHERS THEN code:=SQLCODE; END;
  require(code=-20108,'moving_purchase_after_sale_rejected'); unchanged;
  SELECT operation_order INTO n FROM proxima_owner.transactions WHERE id=b1;
  require(n=1,'failed_reorder_restores_original_order');
  DBMS_OUTPUT.PUT_LINE('PASS invalid_reorder_replays_history_and_rolls_back');
  ROLLBACK TO ordered_fixture;
  SELECT COUNT(*) INTO n FROM proxima_owner.users WHERE id=u; require(n=0,'fixture_owner_removed');
  SELECT COUNT(*) INTO n FROM proxima_owner.audit_log WHERE user_id=u; require(n=0,'fixture_audit_removed');
  SELECT COUNT(*) INTO n FROM proxima_owner.transactions WHERE portfolio_id=p; require(n=0,'fixture_operations_removed');
  DBMS_OUTPUT.PUT_LINE('PASS ordered_fixture_and_audit_fully_rolled_back');
EXCEPTION WHEN OTHERS THEN ROLLBACK; RAISE;
END;
/
