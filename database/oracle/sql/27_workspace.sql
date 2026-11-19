WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_workspace AUTHID DEFINER AS
  PROCEDURE get_settings(p_token VARCHAR2,p_result OUT SYS_REFCURSOR);
  PROCEDURE save_settings(p_token VARCHAR2,p_quote_provider_kind VARCHAR2,p_exchange_rate_provider_kind VARCHAR2,
    p_risk_free_rate_percent NUMBER DEFAULT NULL,p_risk_free_currency VARCHAR2 DEFAULT NULL);
  PROCEDURE list_goals(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR);
  PROCEDURE save_goal(p_token VARCHAR2,p_portfolio_id RAW,p_id IN OUT RAW,p_title VARCHAR2,
    p_target_amount NUMBER,p_currency VARCHAR2,p_monthly_contribution NUMBER,
    p_expected_annual_return NUMBER,p_target_date DATE);
  PROCEDURE archive_goal(p_token VARCHAR2,p_id RAW);
  PROCEDURE project_goal(p_token VARCHAR2,p_id RAW,p_initial_capital NUMBER,p_months NUMBER,p_result OUT SYS_REFCURSOR);
  PROCEDURE list_notifications(p_token VARCHAR2,p_offset NUMBER,p_limit NUMBER,p_result OUT SYS_REFCURSOR);
  PROCEDURE dismiss_notification(p_token VARCHAR2,p_id RAW);
  PROCEDURE list_tags(p_token VARCHAR2,p_result OUT SYS_REFCURSOR);
  PROCEDURE save_tag(p_token VARCHAR2,p_name VARCHAR2,p_id IN OUT RAW);
  PROCEDURE set_asset_tag(p_token VARCHAR2,p_asset_id RAW,p_tag_id RAW,p_enabled NUMBER);
  PROCEDURE delete_tag(p_token VARCHAR2,p_id RAW);
END pkg_workspace;
/
CREATE OR REPLACE PACKAGE BODY pkg_workspace AS
  FUNCTION actor(p_token VARCHAR2) RETURN RAW IS
    result RAW(16); role_value users.role%TYPE;
  BEGIN
    result:=pkg_auth.require_user(p_token);
    SELECT role INTO role_value FROM users WHERE id=result;
    IF role_value NOT IN ('PrivateInvestor','FinancialConsultant') THEN
      RAISE_APPLICATION_ERROR(-20600,'Investor or consultant role required');
    END IF;
    RETURN result;
  END;
  PROCEDURE owned_portfolio(p_user RAW,p_id RAW,p_lock BOOLEAN DEFAULT FALSE) IS
    id_value RAW(16);
  BEGIN
    IF p_lock THEN
      SELECT id INTO id_value FROM portfolios WHERE id=p_id AND owner_user_id=p_user AND is_archived=0 FOR UPDATE;
    ELSE
      SELECT id INTO id_value FROM portfolios WHERE id=p_id AND owner_user_id=p_user AND is_archived=0;
    END IF;
  EXCEPTION WHEN NO_DATA_FOUND THEN RAISE_APPLICATION_ERROR(-20601,'Resource unavailable');
  END;
  PROCEDURE owned_goal(p_user RAW,p_id RAW,p_lock BOOLEAN DEFAULT FALSE) IS
    portfolio_value RAW(16); id_value RAW(16);
  BEGIN
    SELECT portfolio_id INTO portfolio_value FROM goals WHERE id=p_id;
    owned_portfolio(p_user,portfolio_value,p_lock);
    IF p_lock THEN
      SELECT id INTO id_value FROM goals WHERE id=p_id AND portfolio_id=portfolio_value AND is_archived=0 FOR UPDATE;
    ELSE
      SELECT id INTO id_value FROM goals WHERE id=p_id AND portfolio_id=portfolio_value AND is_archived=0;
    END IF;
  EXCEPTION WHEN NO_DATA_FOUND THEN RAISE_APPLICATION_ERROR(-20601,'Resource unavailable');
  END;
  PROCEDURE audit_event(p_user RAW,p_action VARCHAR2,p_id RAW) IS
  BEGIN
    INSERT INTO audit_log(user_id,action,timestamp,metadata_json)
      VALUES(p_user,p_action,SYSTIMESTAMP,JSON_OBJECT('resource_id' VALUE RAWTOHEX(p_id) RETURNING CLOB));
  END;
  PROCEDURE lock_user(p_user RAW) IS
    id_value RAW(16);
  BEGIN
    SELECT id INTO id_value FROM users WHERE id=p_user FOR UPDATE;
  END;
  PROCEDURE get_settings(p_token VARCHAR2,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    OPEN p_result FOR SELECT quote_provider_kind,exchange_rate_provider_kind,
      CASE WHEN quote_api_key IS NULL THEN 0 ELSE 1 END AS quote_api_key_configured,
      risk_free_rate_percent,risk_free_currency
      FROM user_settings WHERE owner_user_id=user_value;
  END;
  PROCEDURE save_settings(p_token VARCHAR2,p_quote_provider_kind VARCHAR2,p_exchange_rate_provider_kind VARCHAR2,
    p_risk_free_rate_percent NUMBER DEFAULT NULL,p_risk_free_currency VARCHAR2 DEFAULT NULL) IS
    user_value RAW(16); quote_value VARCHAR2(255); fx_value VARCHAR2(255); currency_value VARCHAR2(3); count_value NUMBER;
  BEGIN
    SAVEPOINT workspace_api; user_value:=actor(p_token);
    IF TRIM(p_quote_provider_kind) IS NULL OR LENGTH(TRIM(p_quote_provider_kind))>64
      OR TRIM(p_exchange_rate_provider_kind) IS NULL OR LENGTH(TRIM(p_exchange_rate_provider_kind))>64 THEN
      RAISE_APPLICATION_ERROR(-20602,'Provider names must contain 1 to 64 characters');
    END IF;
    quote_value:=TRIM(p_quote_provider_kind); fx_value:=TRIM(p_exchange_rate_provider_kind);
    IF (p_risk_free_rate_percent IS NULL AND p_risk_free_currency IS NOT NULL)
      OR (p_risk_free_rate_percent IS NOT NULL AND p_risk_free_currency IS NULL) THEN
      RAISE_APPLICATION_ERROR(-20602,'Risk-free rate and currency must be supplied together');
    END IF;
    IF p_risk_free_rate_percent IS NOT NULL THEN
      IF ROUND(p_risk_free_rate_percent,6)<=-100 OR ROUND(p_risk_free_rate_percent,6)>=POWER(10,6)
        OR TRIM(p_risk_free_currency) IS NULL OR LENGTH(TRIM(p_risk_free_currency))<>3 THEN
        RAISE_APPLICATION_ERROR(-20602,'Invalid risk-free rate or currency');
      END IF;
      currency_value:=UPPER(TRIM(p_risk_free_currency));
      SELECT COUNT(*) INTO count_value FROM currencies WHERE code=currency_value;
      IF count_value=0 THEN RAISE_APPLICATION_ERROR(-20602,'Unknown risk-free currency'); END IF;
    END IF;
    lock_user(user_value);
    MERGE INTO user_settings s USING (SELECT user_value AS owner_id FROM dual) v ON (s.owner_user_id=v.owner_id)
      WHEN MATCHED THEN UPDATE SET quote_provider_kind=quote_value,exchange_rate_provider_kind=fx_value,
        risk_free_rate_percent=p_risk_free_rate_percent,risk_free_currency=currency_value
      WHEN NOT MATCHED THEN INSERT(owner_user_id,quote_provider_kind,exchange_rate_provider_kind,risk_free_rate_percent,risk_free_currency)
        VALUES(user_value,quote_value,fx_value,p_risk_free_rate_percent,currency_value);
    audit_event(user_value,'SettingsSaved',user_value);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO workspace_api; RAISE;
  END;
  PROCEDURE list_goals(p_token VARCHAR2,p_portfolio_id RAW,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token); owned_portfolio(user_value,p_portfolio_id);
    OPEN p_result FOR SELECT RAWTOHEX(id) AS goal_id,RAWTOHEX(portfolio_id) AS portfolio_id,title,target_amount,currency,
      monthly_contribution,expected_annual_return_percent,target_date,created_at
      FROM goals WHERE portfolio_id=p_portfolio_id AND is_archived=0 ORDER BY created_at,id;
  END;
  PROCEDURE save_goal(p_token VARCHAR2,p_portfolio_id RAW,p_id IN OUT RAW,p_title VARCHAR2,
    p_target_amount NUMBER,p_currency VARCHAR2,p_monthly_contribution NUMBER,
    p_expected_annual_return NUMBER,p_target_date DATE) IS
    user_value RAW(16); old_id RAW(16):=p_id; currency_value currencies.code%TYPE; count_value NUMBER;
  BEGIN
    SAVEPOINT workspace_api; user_value:=actor(p_token); owned_portfolio(user_value,p_portfolio_id,TRUE);
    IF TRIM(p_title) IS NULL OR LENGTH(TRIM(p_title))>255 OR p_target_amount IS NULL OR ROUND(p_target_amount,10)<=0
      OR p_target_amount>=POWER(10,18) OR p_monthly_contribution IS NULL OR p_monthly_contribution<0
      OR p_monthly_contribution>=POWER(10,18) OR p_expected_annual_return IS NULL OR ROUND(p_expected_annual_return,6)<=-100
      OR ROUND(p_expected_annual_return,6)>=POWER(10,6) OR p_currency IS NULL OR LENGTH(TRIM(p_currency))<>3 THEN
      RAISE_APPLICATION_ERROR(-20602,'Invalid goal parameters');
    END IF;
    currency_value:=UPPER(TRIM(p_currency));
    SELECT COUNT(*) INTO count_value FROM currencies WHERE code=currency_value;
    IF count_value=0 THEN RAISE_APPLICATION_ERROR(-20602,'Unknown goal currency'); END IF;
    IF p_id IS NULL THEN
      p_id:=SYS_GUID();
      INSERT INTO goals(id,portfolio_id,title,target_amount,currency,monthly_contribution,expected_annual_return_percent,target_date)
        VALUES(p_id,p_portfolio_id,TRIM(p_title),p_target_amount,currency_value,p_monthly_contribution,p_expected_annual_return,p_target_date);
    ELSE
      UPDATE goals SET title=TRIM(p_title),target_amount=p_target_amount,currency=currency_value,
        monthly_contribution=p_monthly_contribution,expected_annual_return_percent=p_expected_annual_return,target_date=p_target_date
        WHERE id=p_id AND portfolio_id=p_portfolio_id AND is_archived=0;
      IF SQL%ROWCOUNT<>1 THEN RAISE_APPLICATION_ERROR(-20601,'Resource unavailable'); END IF;
    END IF;
    audit_event(user_value,'GoalSaved',p_id);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO workspace_api; p_id:=old_id; RAISE;
  END;
  PROCEDURE archive_goal(p_token VARCHAR2,p_id RAW) IS
    user_value RAW(16);
  BEGIN
    SAVEPOINT workspace_api; user_value:=actor(p_token); owned_goal(user_value,p_id,TRUE);
    UPDATE goals SET is_archived=1 WHERE id=p_id;
    audit_event(user_value,'GoalArchived',p_id);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO workspace_api; RAISE;
  END;
  PROCEDURE project_goal(p_token VARCHAR2,p_id RAW,p_initial_capital NUMBER,p_months NUMBER,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16); annual_value NUMBER; monthly_value NUMBER; target_value NUMBER;
    contribution_value NUMBER; projected_value NUMBER; growth_value NUMBER; currency_value VARCHAR2(3);
    numeric_overflow EXCEPTION; PRAGMA EXCEPTION_INIT(numeric_overflow,-1426);
  BEGIN
    user_value:=actor(p_token); owned_goal(user_value,p_id);
    IF p_initial_capital IS NULL OR p_initial_capital<0 OR p_initial_capital>=POWER(10,18)
      OR p_months IS NULL OR p_months<0 OR p_months>1200 OR p_months<>TRUNC(p_months) THEN
      RAISE_APPLICATION_ERROR(-20602,'Capital must be nonnegative and months an integer from 0 to 1200');
    END IF;
    SELECT expected_annual_return_percent,monthly_contribution,target_amount,currency
      INTO annual_value,contribution_value,target_value,currency_value FROM goals WHERE id=p_id;
    IF annual_value<=-100 OR contribution_value<0 THEN RAISE_APPLICATION_ERROR(-20602,'Invalid stored goal assumptions'); END IF;
    monthly_value:=POWER(1+annual_value/100,1/12)-1;
    BEGIN
      IF p_months=0 THEN projected_value:=p_initial_capital;
      ELSIF annual_value=0 THEN projected_value:=p_initial_capital+contribution_value*p_months;
      ELSE
        growth_value:=POWER(1+monthly_value,p_months);
        projected_value:=p_initial_capital*growth_value+contribution_value*(growth_value-1)/monthly_value;
      END IF;
    EXCEPTION WHEN VALUE_ERROR OR ZERO_DIVIDE OR numeric_overflow THEN RAISE_APPLICATION_ERROR(-20602,'Projection exceeds supported numeric range');
    END;
    OPEN p_result FOR SELECT p_months AS months,p_initial_capital AS initial_capital,contribution_value AS monthly_contribution,
      annual_value AS expected_annual_return_percent,monthly_value AS effective_monthly_rate,
      projected_value AS projected_amount,target_value AS target_amount,currency_value AS currency,
      CASE WHEN projected_value>=target_value THEN 1 ELSE 0 END AS target_reached FROM dual;
  END;
  PROCEDURE list_notifications(p_token VARCHAR2,p_offset NUMBER,p_limit NUMBER,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    IF p_offset IS NULL OR p_offset<0 OR p_offset<>TRUNC(p_offset) OR p_offset>1000000
      OR p_limit IS NULL OR p_limit<1 OR p_limit>200 OR p_limit<>TRUNC(p_limit) THEN
      RAISE_APPLICATION_ERROR(-20602,'Invalid pagination');
    END IF;
    OPEN p_result FOR SELECT RAWTOHEX(id) AS notification_id,title,message,source,severity,created_at_utc
      FROM notifications WHERE user_id=user_value AND deleted_at_utc IS NULL
      ORDER BY created_at_utc DESC,id DESC OFFSET p_offset ROWS FETCH NEXT p_limit ROWS ONLY;
  END;
  PROCEDURE dismiss_notification(p_token VARCHAR2,p_id RAW) IS
    user_value RAW(16);
  BEGIN
    SAVEPOINT workspace_api; user_value:=actor(p_token);
    UPDATE notifications SET deleted_at_utc=SYSTIMESTAMP WHERE id=p_id AND user_id=user_value AND deleted_at_utc IS NULL;
    IF SQL%ROWCOUNT<>1 THEN RAISE_APPLICATION_ERROR(-20601,'Resource unavailable'); END IF;
    audit_event(user_value,'NotificationDismissed',p_id);
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO workspace_api; RAISE;
  END;
  PROCEDURE list_tags(p_token VARCHAR2,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    OPEN p_result FOR SELECT RAWTOHEX(id) AS tag_id,name FROM tags WHERE owner_user_id=user_value ORDER BY name,id;
  END;
  PROCEDURE save_tag(p_token VARCHAR2,p_name VARCHAR2,p_id IN OUT RAW) IS
    user_value RAW(16); old_id RAW(16):=p_id;
  BEGIN
    SAVEPOINT workspace_api; user_value:=actor(p_token);
    IF TRIM(p_name) IS NULL OR LENGTH(TRIM(p_name))>255 THEN RAISE_APPLICATION_ERROR(-20602,'Tag name required, up to 255 characters'); END IF;
    lock_user(user_value);
    IF p_id IS NULL THEN
      p_id:=SYS_GUID(); INSERT INTO tags(id,owner_user_id,name) VALUES(p_id,user_value,TRIM(p_name));
    ELSE
      UPDATE tags SET name=TRIM(p_name) WHERE id=p_id AND owner_user_id=user_value;
      IF SQL%ROWCOUNT<>1 THEN RAISE_APPLICATION_ERROR(-20601,'Resource unavailable'); END IF;
    END IF;
    audit_event(user_value,'TagSaved',p_id);
  EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN ROLLBACK TO workspace_api; p_id:=old_id; RAISE_APPLICATION_ERROR(-20603,'Tag name already exists');
    WHEN OTHERS THEN ROLLBACK TO workspace_api; p_id:=old_id; RAISE;
  END;
  PROCEDURE set_asset_tag(p_token VARCHAR2,p_asset_id RAW,p_tag_id RAW,p_enabled NUMBER) IS
    user_value RAW(16); portfolio_value RAW(16); id_value RAW(16);
  BEGIN
    SAVEPOINT workspace_api; user_value:=actor(p_token);
    IF p_enabled IS NULL OR p_enabled NOT IN (0,1) THEN RAISE_APPLICATION_ERROR(-20602,'Enabled must be 0 or 1'); END IF;
    lock_user(user_value);
    SELECT portfolio_id INTO portfolio_value FROM assets WHERE id=p_asset_id AND is_archived=0;
    owned_portfolio(user_value,portfolio_value,TRUE);
    SELECT id INTO id_value FROM tags WHERE id=p_tag_id AND owner_user_id=user_value FOR UPDATE;
    IF p_enabled=1 THEN
      MERGE INTO asset_tags t USING (SELECT p_asset_id AS asset_id,p_tag_id AS tag_id FROM dual) v
        ON (t.asset_id=v.asset_id AND t.tag_id=v.tag_id)
        WHEN NOT MATCHED THEN INSERT(asset_id,tag_id) VALUES(p_asset_id,p_tag_id);
    ELSE DELETE FROM asset_tags WHERE asset_id=p_asset_id AND tag_id=p_tag_id;
    END IF;
    audit_event(user_value,CASE WHEN p_enabled=1 THEN 'AssetTagEnabled' ELSE 'AssetTagDisabled' END,p_asset_id);
  EXCEPTION
    WHEN NO_DATA_FOUND THEN ROLLBACK TO workspace_api; RAISE_APPLICATION_ERROR(-20601,'Resource unavailable');
    WHEN OTHERS THEN ROLLBACK TO workspace_api; RAISE;
  END;
  PROCEDURE delete_tag(p_token VARCHAR2,p_id RAW) IS
    user_value RAW(16); id_value RAW(16);
  BEGIN
    SAVEPOINT workspace_api; user_value:=actor(p_token); lock_user(user_value);
    SELECT id INTO id_value FROM tags WHERE id=p_id AND owner_user_id=user_value FOR UPDATE;
    DELETE FROM asset_tags WHERE tag_id=p_id;
    DELETE FROM tags WHERE id=p_id AND owner_user_id=user_value;
    audit_event(user_value,'TagDeleted',p_id);
  EXCEPTION
    WHEN NO_DATA_FOUND THEN ROLLBACK TO workspace_api; RAISE_APPLICATION_ERROR(-20601,'Resource unavailable');
    WHEN OTHERS THEN ROLLBACK TO workspace_api; RAISE;
  END;
END pkg_workspace;
/
