-- Запуск: SYS в FREEPDB1; пакет 40_ledger.sql уже установлен.
-- SYS нужен также для проверки эффективных грантов через DBA_* представления.
-- Только временные числовые фикстуры: ни COMMIT, ни DDL, ни рыночные цены.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
DECLARE
  v_owner_id RAW(16) := SYS_GUID();
  v_portfolio_id RAW(16);
  instrument_id RAW(16) := HEXTORAW('00000000000000000000000000000001');
  deposit_id RAW(16); buy1_id RAW(16); buy2_id RAW(16); sell_id RAW(16); rejected_id RAW(16);
  t0 TIMESTAMP WITH TIME ZONE := TO_TIMESTAMP_TZ('2026-01-01 12:00:00 +00:00','YYYY-MM-DD HH24:MI:SS TZH:TZM');
  n NUMBER; actual NUMBER; caught NUMBER;
  c SYS_REFCURSOR; currency_code VARCHAR2(3); cash_balance NUMBER;
  sale_hex VARCHAR2(32); asset_hex VARCHAR2(32); sale_time TIMESTAMP WITH TIME ZONE;
  net_proceeds NUMBER; fifo_cost NUMBER; realized_pnl NUMBER;

  PROCEDURE require(p_condition BOOLEAN,p_case VARCHAR2) IS
  BEGIN
    IF p_condition IS NULL OR NOT p_condition THEN
      RAISE_APPLICATION_ERROR(-20990,'Ledger assertion failed: '||p_case);
    END IF;
  END;

  PROCEDURE check_cash(p_expected NUMBER) IS
  BEGIN
    proxima_owner.pkg_ledger.get_balances(v_owner_id,v_portfolio_id,c);
    FETCH c INTO currency_code,cash_balance;
    require(c%FOUND,'cash_row_exists');
    require(currency_code='USD' AND cash_balance=p_expected,'cash_amount_and_currency');
    FETCH c INTO currency_code,cash_balance;
    require(c%NOTFOUND,'one_cash_currency');
    CLOSE c;
  END;

  PROCEDURE check_fifo(p_cost NUMBER,p_profit NUMBER) IS
  BEGIN
    proxima_owner.pkg_ledger.get_realized_pnl(v_owner_id,v_portfolio_id,c);
    FETCH c INTO sale_hex,asset_hex,currency_code,sale_time,net_proceeds,fifo_cost,realized_pnl;
    require(c%FOUND,'realized_sale_exists');
    require(sale_hex=RAWTOHEX(sell_id) AND LENGTH(asset_hex)=32,'sale_and_asset_identity');
    require(currency_code='USD' AND sale_time=t0+INTERVAL '3' DAY,'sale_currency_and_time');
    require(net_proceeds=1794 AND fifo_cost=p_cost AND realized_pnl=p_profit,'fifo_cost_net_and_profit');
    FETCH c INTO sale_hex,asset_hex,currency_code,sale_time,net_proceeds,fifo_cost,realized_pnl;
    require(c%NOTFOUND,'one_realized_sale');
    CLOSE c;
  END;

  PROCEDURE check_unchanged IS
  BEGIN
    SELECT COUNT(*) INTO n FROM proxima_owner.transactions t WHERE t.portfolio_id=v_portfolio_id;
    require(n=4,'rejection_preserves_transaction_count');
    SELECT COUNT(*) INTO n FROM proxima_owner.audit_log a WHERE a.user_id=v_owner_id;
    require(n=5,'rejection_preserves_audit_count');
    SELECT quantity INTO actual FROM proxima_owner.transactions WHERE id=buy1_id;
    require(actual=10,'rejection_preserves_original_purchase');
    SELECT quantity INTO actual FROM proxima_owner.assets a WHERE a.portfolio_id=v_portfolio_id;
    require(actual=8,'rejection_preserves_position_quantity');
    SELECT average_buy_price INTO actual FROM proxima_owner.assets a WHERE a.portfolio_id=v_portfolio_id;
    require(actual=121,'rejection_preserves_position_cost');
    check_cash(9574);
    check_fifo(1252,542);
  END;
BEGIN
  SAVEPOINT ledger_test_fixture;
  INSERT INTO proxima_owner.users(id,login,display_name,role,password_hash,account_status)
    VALUES(v_owner_id,'ledger-test-'||RAWTOHEX(v_owner_id),'Rollback ledger fixture','PrivateInvestor','not-a-login-credential','Active');
  proxima_owner.pkg_ledger.create_portfolio(v_owner_id,'Rollback FIFO fixture','USD',v_portfolio_id);
  proxima_owner.pkg_ledger.post_cash(v_owner_id,v_portfolio_id,'Deposit',t0,10000,'USD',0,deposit_id);
  proxima_owner.pkg_ledger.post_trade(v_owner_id,v_portfolio_id,instrument_id,'Buy',t0+INTERVAL '1' DAY,10,100,10,buy1_id);
  proxima_owner.pkg_ledger.post_trade(v_owner_id,v_portfolio_id,instrument_id,'Buy',t0+INTERVAL '2' DAY,10,120,10,buy2_id);
  proxima_owner.pkg_ledger.post_trade(v_owner_id,v_portfolio_id,instrument_id,'Sell',t0+INTERVAL '3' DAY,12,150,6,sell_id);
  check_unchanged;
  SELECT quantity*average_buy_price INTO actual FROM proxima_owner.assets a WHERE a.portfolio_id=v_portfolio_id;
  require(actual=968,'fifo_remaining_eight_units_basis_968');
  DBMS_OUTPUT.PUT_LINE('PASS fifo_two_lots_partial_sale_with_fees');

  caught:=0;
  BEGIN
    proxima_owner.pkg_ledger.post_trade(v_owner_id,v_portfolio_id,instrument_id,'Sell',t0+INTERVAL '4' DAY,9,150,0,rejected_id);
  EXCEPTION WHEN OTHERS THEN caught:=SQLCODE;
  END;
  require(caught=-20108,'oversell_rejected_with_business_error');
  check_unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS oversell_is_atomic');

  caught:=0;
  BEGIN
    proxima_owner.pkg_ledger.post_cash(v_owner_id,v_portfolio_id,'Withdrawal',t0+INTERVAL '4' DAY,9575,'USD',0,rejected_id);
  EXCEPTION WHEN OTHERS THEN caught:=SQLCODE;
  END;
  require(caught=-20109,'negative_cash_rejected_with_business_error');
  check_unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS negative_cash_is_atomic');

  caught:=0;
  BEGIN
    proxima_owner.pkg_ledger.update_transaction(v_owner_id,buy1_id,t0+INTERVAL '1' DAY,1,100,100,10);
  EXCEPTION WHEN OTHERS THEN caught:=SQLCODE;
  END;
  require(caught=-20108,'old_purchase_edit_revalidates_future_sale');
  check_unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS invalid_old_purchase_edit_rolls_back');

  -- Итоговый баланс был бы положительным, но в момент покупок денег недостаточно.
  caught:=0;
  BEGIN
    proxima_owner.pkg_ledger.post_cash(v_owner_id,v_portfolio_id,'Withdrawal',t0+INTERVAL '12' HOUR,9500,'USD',0,rejected_id);
  EXCEPTION WHEN OTHERS THEN caught:=SQLCODE;
  END;
  require(caught=-20109,'earlier_withdrawal_checks_entire_history');
  check_unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS backdated_cash_revalidates_history');

  caught:=0;
  BEGIN
    proxima_owner.pkg_ledger.post_cash(v_owner_id,v_portfolio_id,'Deposit',t0+INTERVAL '1' DAY,1,'USD',0,rejected_id);
  EXCEPTION WHEN OTHERS THEN caught:=SQLCODE;
  END;
  require(caught=-20103,'identical_timestamp_rejected_explicitly');
  check_unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS ambiguous_timestamp_rejected');

  caught:=0;
  BEGIN
    proxima_owner.pkg_ledger.remove_transaction(v_owner_id,buy1_id);
  EXCEPTION WHEN OTHERS THEN caught:=SQLCODE;
  END;
  require(caught=-20108,'delete_purchase_revalidates_future_sale');
  check_unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS referenced_purchase_delete_rolls_back');

  SAVEPOINT ledger_before_price_edit;
  proxima_owner.pkg_ledger.update_transaction(v_owner_id,buy2_id,t0+INTERVAL '2' DAY,10,130,1300,10);
  check_cash(9474);
  check_fifo(1272,522);
  SELECT quantity,average_buy_price INTO n,actual FROM proxima_owner.assets a WHERE a.portfolio_id=v_portfolio_id;
  require(n=8 AND actual=131 AND n*actual=1048,'price_edit_recomputes_remaining_position');
  SELECT COUNT(*) INTO n FROM proxima_owner.audit_log a WHERE a.user_id=v_owner_id;
  require(n=6,'successful_edit_adds_exactly_one_audit_record');
  SELECT COUNT(*) INTO n FROM proxima_owner.audit_log a
    WHERE a.user_id=v_owner_id AND JSON_VALUE(a.metadata_json,'$.transaction_id')=RAWTOHEX(buy2_id)
      AND JSON_VALUE(a.metadata_json,'$.before.price' RETURNING NUMBER)=120
      AND JSON_VALUE(a.metadata_json,'$.after.price' RETURNING NUMBER)=130;
  require(n=1,'audit_contains_old_and_new_price');
  ROLLBACK TO ledger_before_price_edit;
  check_unchanged;
  DBMS_OUTPUT.PUT_LINE('PASS price_edit_recomputes_and_audit_rolls_back');

  SELECT COUNT(*) INTO n FROM dba_tab_privs
    WHERE owner='PROXIMA_OWNER' AND table_name='PKG_LEDGER'
      AND privilege='EXECUTE' AND (grantee IN ('PROXIMA_APP','PUBLIC') OR grantee IN
        (SELECT granted_role FROM dba_role_privs START WITH grantee='PROXIMA_APP'
          CONNECT BY NOCYCLE PRIOR granted_role=grantee));
  require(n=0,'app_has_no_internal_ledger_execute_direct_public_or_role');
  SELECT COUNT(*) INTO n FROM dba_tab_privs
    WHERE owner='PROXIMA_OWNER' AND privilege IN ('INSERT','UPDATE','DELETE')
      AND (grantee IN ('PROXIMA_APP','PUBLIC') OR grantee IN
        (SELECT granted_role FROM dba_role_privs START WITH grantee='PROXIMA_APP'
          CONNECT BY NOCYCLE PRIOR granted_role=grantee));
  require(n=0,'app_has_no_private_table_writes');
  SELECT COUNT(*) INTO n FROM dba_sys_privs
    WHERE privilege IN ('EXECUTE ANY PROCEDURE','INSERT ANY TABLE','UPDATE ANY TABLE','DELETE ANY TABLE')
      AND (grantee IN ('PROXIMA_APP','PUBLIC') OR grantee IN
        (SELECT granted_role FROM dba_role_privs START WITH grantee='PROXIMA_APP'
          CONNECT BY NOCYCLE PRIOR granted_role=grantee));
  require(n=0,'app_has_no_system_privilege_bypass');
  DBMS_OUTPUT.PUT_LINE('PASS application_has_no_private_ledger_write_access');

  ROLLBACK TO ledger_test_fixture;
  SELECT COUNT(*) INTO n FROM proxima_owner.users WHERE id=v_owner_id;
  require(n=0,'fixture_owner_rolled_back');
  SELECT COUNT(*) INTO n FROM proxima_owner.portfolios WHERE id=v_portfolio_id;
  require(n=0,'fixture_portfolio_rolled_back');
  SELECT COUNT(*) INTO n FROM proxima_owner.transactions t WHERE t.portfolio_id=v_portfolio_id;
  require(n=0,'fixture_transactions_rolled_back');
  SELECT COUNT(*) INTO n FROM proxima_owner.assets a WHERE a.portfolio_id=v_portfolio_id;
  require(n=0,'fixture_position_rolled_back');
  SELECT COUNT(*) INTO n FROM proxima_owner.audit_log a WHERE a.user_id=v_owner_id;
  require(n=0,'fixture_audit_rolled_back_no_autonomous_commit');
  DBMS_OUTPUT.PUT_LINE('PASS caller_rollback_removes_all_fixture_data_and_audit');
  DBMS_OUTPUT.PUT_LINE('Ledger assertions passed; no fixture data committed.');
EXCEPTION
  WHEN OTHERS THEN
    ROLLBACK;
    RAISE;
END;
/
