WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_admin AUTHID DEFINER AS
  PROCEDURE list_users(p_token VARCHAR2,p_result OUT SYS_REFCURSOR);
  PROCEDURE set_role(p_token VARCHAR2,p_user_id RAW,p_role VARCHAR2);
  PROCEDURE set_status(p_token VARCHAR2,p_user_id RAW,p_status VARCHAR2);
  PROCEDURE list_audit(p_token VARCHAR2,p_offset NUMBER,p_limit NUMBER,p_result OUT SYS_REFCURSOR);
  PROCEDURE list_archived_portfolios(p_token VARCHAR2,p_result OUT SYS_REFCURSOR);
  PROCEDURE restore_portfolio(p_token VARCHAR2,p_id RAW);
  PROCEDURE delete_archived_portfolio(p_token VARCHAR2,p_id RAW);
  PROCEDURE get_statistics(p_token VARCHAR2,p_result OUT SYS_REFCURSOR);
END pkg_admin;
/
CREATE OR REPLACE PACKAGE BODY pkg_admin AS
  FUNCTION actor(p_token VARCHAR2) RETURN RAW IS
    result RAW(16);
  BEGIN
    pkg_auth.begin_request(p_token); result:=pkg_auth.require_admin(p_token); RETURN result;
  END;
  PROCEDURE lock_admins IS
  BEGIN
    FOR r IN (SELECT id FROM users WHERE role='Admin' ORDER BY id FOR UPDATE) LOOP NULL; END LOOP;
  END;
  PROCEDURE audit_change(p_user RAW,p_action VARCHAR2,p_target RAW,p_before CLOB,p_after CLOB) IS
  BEGIN
    INSERT INTO audit_log(user_id,action,timestamp,metadata_json)
      VALUES(p_user,p_action,SYSTIMESTAMP,JSON_OBJECT('target_id' VALUE RAWTOHEX(p_target),
        'before' VALUE p_before FORMAT JSON,'after' VALUE p_after FORMAT JSON RETURNING CLOB));
  END;
  FUNCTION user_details(p_id RAW) RETURN CLOB IS
    result CLOB;
  BEGIN
    SELECT JSON_OBJECT('role' VALUE role,'account_status' VALUE account_status RETURNING CLOB)
      INTO result FROM users WHERE id=p_id;
    RETURN result;
  END;
  PROCEDURE list_users(p_token VARCHAR2,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    OPEN p_result FOR SELECT RAWTOHEX(id) AS user_id,login,display_name,role,account_status,
      failed_unlock_attempts,created_at,updated_at FROM users ORDER BY login,id;
  END;
  PROCEDURE set_role(p_token VARCHAR2,p_user_id RAW,p_role VARCHAR2) IS
    user_value RAW(16); old_role VARCHAR2(255); status_value VARCHAR2(255); n NUMBER; before_value CLOB;
  BEGIN
    SAVEPOINT admin_api; user_value:=actor(p_token);
    IF p_role IS NULL OR p_role NOT IN ('Admin','PrivateInvestor','FinancialConsultant') THEN RAISE_APPLICATION_ERROR(-20500,'Unsupported role'); END IF;
    lock_admins; user_value:=actor(p_token);
    SELECT role,account_status INTO old_role,status_value FROM users WHERE id=p_user_id FOR UPDATE;
    IF old_role='Admin' AND status_value='Active' AND p_role<>'Admin' THEN
      SELECT COUNT(*) INTO n FROM users WHERE role='Admin' AND account_status='Active';
      IF n<=1 THEN RAISE_APPLICATION_ERROR(-20501,'Last active administrator must remain'); END IF;
    END IF;
    before_value:=user_details(p_user_id);
    UPDATE users SET role=p_role,updated_at=SYSTIMESTAMP WHERE id=p_user_id;
    IF old_role<>p_role THEN UPDATE auth_sessions SET revoked_at=SYSTIMESTAMP WHERE user_id=p_user_id AND revoked_at IS NULL; END IF;
    audit_change(user_value,'UserRoleChanged',p_user_id,before_value,user_details(p_user_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO admin_api; RAISE;
  END;
  PROCEDURE set_status(p_token VARCHAR2,p_user_id RAW,p_status VARCHAR2) IS
    user_value RAW(16); role_value VARCHAR2(255); old_status VARCHAR2(255); n NUMBER; before_value CLOB;
  BEGIN
    SAVEPOINT admin_api; user_value:=actor(p_token);
    IF p_status IS NULL OR p_status NOT IN ('Active','Blocked') THEN RAISE_APPLICATION_ERROR(-20502,'Unsupported account status'); END IF;
    lock_admins; user_value:=actor(p_token);
    SELECT role,account_status INTO role_value,old_status FROM users WHERE id=p_user_id FOR UPDATE;
    IF role_value='Admin' AND old_status='Active' AND p_status='Blocked' THEN
      SELECT COUNT(*) INTO n FROM users WHERE role='Admin' AND account_status='Active';
      IF n<=1 THEN RAISE_APPLICATION_ERROR(-20501,'Last active administrator must remain'); END IF;
    END IF;
    before_value:=user_details(p_user_id);
    UPDATE users SET account_status=p_status,failed_unlock_attempts=CASE WHEN p_status='Active' THEN 0 ELSE failed_unlock_attempts END,
      updated_at=SYSTIMESTAMP WHERE id=p_user_id;
    IF p_status='Blocked' THEN UPDATE auth_sessions SET revoked_at=SYSTIMESTAMP WHERE user_id=p_user_id AND revoked_at IS NULL; END IF;
    audit_change(user_value,'UserStatusChanged',p_user_id,before_value,user_details(p_user_id));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO admin_api; RAISE;
  END;
  PROCEDURE list_audit(p_token VARCHAR2,p_offset NUMBER,p_limit NUMBER,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    IF p_offset IS NULL OR p_offset<0 OR p_offset<>TRUNC(p_offset) OR p_limit IS NULL
      OR p_limit<1 OR p_limit>100 OR p_limit<>TRUNC(p_limit) THEN RAISE_APPLICATION_ERROR(-20503,'Invalid audit page'); END IF;
    OPEN p_result FOR SELECT RAWTOHEX(id) AS audit_id,RAWTOHEX(user_id) AS user_id,action,timestamp,metadata_json
      FROM audit_log ORDER BY timestamp DESC,id DESC OFFSET p_offset ROWS FETCH NEXT p_limit ROWS ONLY;
  END;
  PROCEDURE list_archived_portfolios(p_token VARCHAR2,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    OPEN p_result FOR SELECT RAWTOHEX(p.id) AS portfolio_id,RAWTOHEX(p.owner_user_id) AS owner_user_id,
      u.login,p.name,p.description,p.client_label,p.base_currency,p.updated_at FROM portfolios p JOIN users u ON u.id=p.owner_user_id
      WHERE p.is_archived=1 ORDER BY p.updated_at DESC,p.id;
  END;
  PROCEDURE restore_portfolio(p_token VARCHAR2,p_id RAW) IS
    user_value RAW(16); archived NUMBER; before_value CLOB; after_value CLOB;
  BEGIN
    SAVEPOINT admin_api; user_value:=actor(p_token);
    SELECT is_archived INTO archived FROM portfolios WHERE id=p_id FOR UPDATE;
    IF archived<>1 THEN RAISE_APPLICATION_ERROR(-20504,'Archived portfolio required'); END IF;
    SELECT JSON_OBJECT('name' VALUE name,'is_archived' VALUE is_archived RETURNING CLOB) INTO before_value FROM portfolios WHERE id=p_id;
    UPDATE portfolios SET is_archived=0,updated_at=SYSTIMESTAMP WHERE id=p_id;
    SELECT JSON_OBJECT('name' VALUE name,'is_archived' VALUE is_archived RETURNING CLOB) INTO after_value FROM portfolios WHERE id=p_id;
    audit_change(user_value,'PortfolioRestored',p_id,before_value,after_value);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO admin_api; RAISE;
  END;
  PROCEDURE delete_archived_portfolio(p_token VARCHAR2,p_id RAW) IS
    user_value RAW(16); archived NUMBER; before_value CLOB;
  BEGIN
    SAVEPOINT admin_api; user_value:=actor(p_token);
    SELECT is_archived INTO archived FROM portfolios WHERE id=p_id FOR UPDATE;
    IF archived<>1 THEN RAISE_APPLICATION_ERROR(-20504,'Archived portfolio required'); END IF;
    SELECT JSON_OBJECT('owner_user_id' VALUE RAWTOHEX(owner_user_id),'name' VALUE name,'description' VALUE description,
      'client_label' VALUE client_label,'base_currency' VALUE base_currency,'is_archived' VALUE is_archived RETURNING CLOB)
      INTO before_value FROM portfolios WHERE id=p_id;
    DELETE FROM transactions WHERE portfolio_id=p_id;
    DELETE FROM import_rows WHERE batch_id IN (SELECT id FROM import_batches WHERE portfolio_id=p_id);
    DELETE FROM import_batches WHERE portfolio_id=p_id;
    DELETE FROM goals WHERE portfolio_id=p_id;
    DELETE FROM portfolio_snapshots WHERE portfolio_id=p_id;
    DELETE FROM asset_tags WHERE asset_id IN (SELECT id FROM assets WHERE portfolio_id=p_id);
    DELETE FROM assets WHERE portfolio_id=p_id;
    DELETE FROM portfolios WHERE id=p_id;
    audit_change(user_value,'PortfolioPermanentlyDeleted',p_id,before_value,NULL);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO admin_api; RAISE;
  END;
  PROCEDURE get_statistics(p_token VARCHAR2,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    OPEN p_result FOR SELECT (SELECT COUNT(*) FROM users) AS user_count,
      (SELECT COUNT(*) FROM users WHERE account_status='Active') AS active_user_count,
      (SELECT COUNT(*) FROM portfolios WHERE is_archived=0) AS active_portfolio_count,
      (SELECT COUNT(*) FROM portfolios WHERE is_archived=1) AS archived_portfolio_count,
      (SELECT COUNT(*) FROM transactions) AS transaction_count,
      (SELECT COUNT(*) FROM audit_log) AS audit_count FROM dual;
  END;
END pkg_admin;
/
