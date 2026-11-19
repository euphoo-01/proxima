WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET LINESIZE 180 PAGESIZE 100
VARIABLE cash_result REFCURSOR
VARIABLE profit_result REFCURSOR
VARIABLE position_result REFCURSOR
DECLARE
  owner_id RAW(16):=SYS_GUID();
  v_portfolio_id RAW(16);
  operation_id RAW(16);
  instrument_id RAW(16):=HEXTORAW('00000000000000000000000000000001');
  started TIMESTAMP WITH TIME ZONE:=TO_TIMESTAMP_TZ('2026-11-01 12:00:00 +00:00','YYYY-MM-DD HH24:MI:SS TZH:TZM');
BEGIN
  INSERT INTO proxima_owner.users(id,login,display_name,role,password_hash,account_status)
    VALUES(owner_id,'presentation-'||RAWTOHEX(owner_id),'Инвестор','PrivateInvestor','not-a-login-credential','Active');
  proxima_owner.pkg_ledger.create_portfolio(owner_id,'Личный портфель','USD',v_portfolio_id);
  proxima_owner.pkg_ledger.post_cash(owner_id,v_portfolio_id,'Deposit',started,10000,'USD',0,operation_id);
  proxima_owner.pkg_ledger.post_trade(owner_id,v_portfolio_id,instrument_id,'Buy',started+INTERVAL '1' DAY,10,100,10,operation_id);
  proxima_owner.pkg_ledger.post_trade(owner_id,v_portfolio_id,instrument_id,'Buy',started+INTERVAL '2' DAY,10,120,10,operation_id);
  proxima_owner.pkg_ledger.post_trade(owner_id,v_portfolio_id,instrument_id,'Sell',started+INTERVAL '3' DAY,12,150,6,operation_id);
  proxima_owner.pkg_ledger.get_balances(owner_id,v_portfolio_id,:cash_result);
  proxima_owner.pkg_ledger.get_realized_pnl(owner_id,v_portfolio_id,:profit_result);
  OPEN :position_result FOR
    SELECT i.ticker,a.quantity,a.average_buy_price,a.quantity*a.average_buy_price AS remaining_cost
    FROM proxima_owner.assets a JOIN proxima_owner.instruments i ON i.id=a.instrument_id
    WHERE a.portfolio_id=v_portfolio_id;
  DBMS_OUTPUT.PUT_LINE('Пополнение: 10000 USD. Покупки: 10 x 100 + 10 комиссии; 10 x 120 + 10 комиссии.');
  DBMS_OUTPUT.PUT_LINE('Продажа: 12 x 150 - 6 комиссии. Данные ниже рассчитаны процедурами Oracle.');
  DBMS_OUTPUT.PUT_LINE('Цены — числовой пример сделок, а не текущие рыночные котировки.');
EXCEPTION WHEN OTHERS THEN
  ROLLBACK;
  RAISE;
END;
/
PRINT cash_result
PRINT profit_result
PRINT position_result
ROLLBACK;
PROMPT Все изменения примера отменены после просмотра результатов.
