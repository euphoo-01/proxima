WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET DEFINE OFF
ALTER SESSION SET CURRENT_SCHEMA=PROXIMA_OWNER;
DECLARE
  user_a RAW(16):=SYS_GUID(); user_b RAW(16):=SYS_GUID(); admin_id RAW(16):=SYS_GUID();
  token_a VARCHAR2(64):=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32));
  token_b VARCHAR2(64):=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32));
  token_admin VARCHAR2(64):=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32));
  portfolio_a RAW(16):=SYS_GUID(); portfolio_b RAW(16):=SYS_GUID();
  asset_a RAW(16):=SYS_GUID(); asset_b RAW(16):=SYS_GUID(); instrument_value RAW(16);
  notification_a RAW(16):=SYS_GUID(); notification_b RAW(16):=SYS_GUID();
  goal_id RAW(16); other_goal RAW(16); tag_a RAW(16); tag_b RAW(16); temporary_id RAW(16);
  cursor_value SYS_REFCURSOR; count_value NUMBER; code_value NUMBER;
  id_text VARCHAR2(32); title_value VARCHAR2(255); provider_value VARCHAR2(255); fx_value VARCHAR2(255);
  configured NUMBER; risk_value NUMBER; risk_currency VARCHAR2(3); body_value CLOB; source_value VARCHAR2(255); severity_value VARCHAR2(255);
  timestamp_value TIMESTAMP WITH TIME ZONE;
  PROCEDURE check_true(p_value BOOLEAN,p_name VARCHAR2) IS
  BEGIN
    IF p_value IS NULL OR NOT p_value THEN RAISE_APPLICATION_ERROR(-20991,p_name); END IF;
    DBMS_OUTPUT.PUT_LINE('PASS '||p_name);
  END;
  PROCEDURE fixture_user(p_user RAW,p_role VARCHAR2,p_token VARCHAR2) IS
  BEGIN
    INSERT INTO users(id,login,display_name,role,password_hash,account_status)
      VALUES(p_user,'ws_'||RAWTOHEX(p_user),'Workspace fixture',p_role,'unusable-fixture-password','Active');
    INSERT INTO auth_sessions(user_id,token_hash,expires_at)
      VALUES(p_user,DBMS_CRYPTO.HASH(HEXTORAW(p_token),DBMS_CRYPTO.HASH_SH256),SYSTIMESTAMP+INTERVAL '1' HOUR);
  END;
  PROCEDURE project(p_capital NUMBER,p_months NUMBER,p_expected NUMBER,p_name VARCHAR2) IS
    months_value NUMBER; capital_value NUMBER; contribution_value NUMBER; annual_value NUMBER;
    monthly_value NUMBER; projected_value NUMBER; target_value NUMBER; currency_value VARCHAR2(3); reached_value NUMBER;
  BEGIN
    pkg_workspace.project_goal(token_a,goal_id,p_capital,p_months,cursor_value);
    FETCH cursor_value INTO months_value,capital_value,contribution_value,annual_value,monthly_value,
      projected_value,target_value,currency_value,reached_value;
    check_true(cursor_value%FOUND AND ABS(projected_value-p_expected)<0.0001 AND months_value=p_months
      AND capital_value=p_capital AND currency_value='USD'
      AND reached_value=CASE WHEN projected_value>=target_value THEN 1 ELSE 0 END,p_name);
    CLOSE cursor_value;
  END;
  PROCEDURE reject_case(p_case PLS_INTEGER,p_expected NUMBER,p_name VARCHAR2) IS
  BEGIN
    code_value:=0;
    BEGIN
      CASE p_case
        WHEN 1 THEN pkg_workspace.list_tags('invalid-token',cursor_value);
        WHEN 2 THEN pkg_workspace.list_tags(token_admin,cursor_value);
        WHEN 3 THEN pkg_workspace.list_goals(token_a,portfolio_b,cursor_value);
        WHEN 4 THEN pkg_workspace.archive_goal(token_b,goal_id);
        WHEN 5 THEN pkg_workspace.project_goal(token_b,goal_id,100,12,cursor_value);
        WHEN 6 THEN pkg_workspace.dismiss_notification(token_a,notification_b);
        WHEN 7 THEN temporary_id:=tag_b; pkg_workspace.save_tag(token_a,'Changed foreign',temporary_id);
        WHEN 8 THEN pkg_workspace.set_asset_tag(token_a,asset_b,tag_a,1);
        WHEN 9 THEN pkg_workspace.set_asset_tag(token_a,asset_a,tag_b,1);
        WHEN 10 THEN pkg_workspace.delete_tag(token_a,tag_b);
        WHEN 11 THEN pkg_workspace.project_goal(token_a,goal_id,100,1201,cursor_value);
        WHEN 12 THEN pkg_workspace.project_goal(token_a,goal_id,100,-1,cursor_value);
        WHEN 13 THEN pkg_workspace.project_goal(token_a,goal_id,100,1.5,cursor_value);
        WHEN 14 THEN pkg_workspace.project_goal(token_a,goal_id,-1,12,cursor_value);
        WHEN 15 THEN temporary_id:=NULL; pkg_workspace.save_goal(token_a,portfolio_a,temporary_id,'Invalid',100,'USD',0,-100,NULL);
        WHEN 16 THEN temporary_id:=NULL; pkg_workspace.save_tag(token_a,'Long term',temporary_id);
        WHEN 17 THEN pkg_workspace.list_notifications(token_a,0,201,cursor_value);
        WHEN 18 THEN pkg_workspace.save_settings(token_a,' ', 'Nbrb');
        WHEN 19 THEN temporary_id:=goal_id; pkg_workspace.save_goal(token_a,portfolio_b,temporary_id,'Moved',100,'USD',0,0,NULL);
        WHEN 20 THEN pkg_workspace.project_goal(token_a,goal_id,100,12,cursor_value);
        WHEN 21 THEN pkg_workspace.list_tags(token_a,cursor_value);
        WHEN 22 THEN pkg_workspace.set_asset_tag(token_a,asset_a,tag_a,NULL);
        WHEN 23 THEN temporary_id:=NULL; pkg_workspace.save_goal(token_a,portfolio_a,temporary_id,'Rounded',0.000000000001,'USD',0,0,NULL);
        WHEN 24 THEN temporary_id:=NULL; pkg_workspace.save_goal(token_a,portfolio_a,temporary_id,'Rounded rate',100,'USD',0,-99.9999999,NULL);
        WHEN 25 THEN pkg_workspace.save_settings(token_a,'Alpaca','Nbrb',5,NULL);
        WHEN 26 THEN pkg_workspace.save_settings(token_a,'Alpaca','Nbrb',NULL,'USD');
        WHEN 27 THEN pkg_workspace.save_settings(token_a,'Alpaca','Nbrb',-100,'USD');
        WHEN 28 THEN pkg_workspace.save_settings(token_a,'Alpaca','Nbrb',5,'ZZZ');
      END CASE;
    EXCEPTION WHEN OTHERS THEN code_value:=SQLCODE;
    END;
    check_true(code_value=p_expected,p_name);
  END;
BEGIN
  check_true(SYS_CONTEXT('USERENV','SESSION_USER')='SYS' AND SYS_CONTEXT('USERENV','CON_NAME')<>'CDB$ROOT','workspace_trusted_fixture_session');
  SAVEPOINT workspace_fixtures;
  fixture_user(user_a,'PrivateInvestor',token_a);
  fixture_user(user_b,'FinancialConsultant',token_b);
  fixture_user(admin_id,'Admin',token_admin);
  INSERT INTO portfolios(id,owner_user_id,name,base_currency) VALUES(portfolio_a,user_a,'Workspace A','USD');
  INSERT INTO portfolios(id,owner_user_id,name,base_currency) VALUES(portfolio_b,user_b,'Workspace B','USD');
  SELECT id INTO instrument_value FROM instruments WHERE ticker='AAPL' AND ROWNUM=1;
  INSERT INTO assets(id,portfolio_id,instrument_id,quantity) VALUES(asset_a,portfolio_a,instrument_value,0);
  INSERT INTO assets(id,portfolio_id,instrument_id,quantity) VALUES(asset_b,portfolio_b,instrument_value,0);
  INSERT INTO notifications(id,user_id,title,message,source,severity)
    VALUES(notification_a,user_a,'Own notification','Own message','Workspace','Info');
  INSERT INTO notifications(id,user_id,title,message,source,severity)
    VALUES(notification_b,user_b,'Foreign notification','Foreign message','Workspace','Info');

  reject_case(1,-20301,'workspace_rejects_invalid_token');
  reject_case(2,-20600,'workspace_rejects_admin_as_portfolio_actor');
  pkg_workspace.save_settings(token_a,'Composite','Nbrb');
  UPDATE user_settings SET quote_api_key='workspace-key-must-never-be-returned' WHERE owner_user_id=user_a;
  pkg_workspace.save_settings(token_a,'Alpaca','Nbrb',5,'usd');
  pkg_workspace.get_settings(token_a,cursor_value);
  FETCH cursor_value INTO provider_value,fx_value,configured,risk_value,risk_currency;
  check_true(cursor_value%FOUND AND provider_value='Alpaca' AND fx_value='Nbrb' AND configured=1
    AND risk_value=5 AND risk_currency='USD','workspace_settings_returns_preferences_without_secret');
  CLOSE cursor_value;
  SELECT COUNT(*) INTO count_value FROM user_settings WHERE owner_user_id=user_a AND quote_api_key='workspace-key-must-never-be-returned';
  check_true(count_value=1,'workspace_settings_preserves_existing_secret_without_rewriting');
  pkg_workspace.get_settings(token_b,cursor_value);
  FETCH cursor_value INTO provider_value,fx_value,configured,risk_value,risk_currency;
  check_true(cursor_value%NOTFOUND,'workspace_settings_does_not_leak_other_owner'); CLOSE cursor_value;
  pkg_workspace.save_settings(token_b,'Binance','Nbrb');
  reject_case(18,-20602,'workspace_rejects_empty_provider');
  reject_case(25,-20602,'workspace_risk_rate_requires_currency');
  reject_case(26,-20602,'workspace_risk_currency_requires_rate');
  reject_case(27,-20602,'workspace_risk_rate_rejects_minus_100');
  reject_case(28,-20602,'workspace_risk_rate_requires_known_currency');
  pkg_workspace.save_settings(token_a,'Alpaca','Nbrb');
  SELECT COUNT(*) INTO count_value FROM user_settings WHERE owner_user_id=user_a
    AND risk_free_rate_percent IS NULL AND risk_free_currency IS NULL;
  check_true(count_value=1,'workspace_full_settings_save_clears_optional_rate_pair');

  pkg_workspace.save_goal(token_a,portfolio_a,goal_id,'Capital',1000,'USD',100,0,NULL);
  project(1000,0,1000,'workspace_projection_zero_horizon');
  project(1000,12,2200,'workspace_projection_zero_rate_end_month_contributions');
  project(1000,1200,121000,'workspace_projection_maximum_horizon');
  pkg_workspace.save_goal(token_a,portfolio_a,goal_id,'Capital',2000,'USD',0,12,NULL);
  project(1000,12,1120,'workspace_projection_effective_annual_growth');
  pkg_workspace.save_goal(token_a,portfolio_a,goal_id,'Capital',2000,'USD',100,12.682503,NULL);
  project(1000,2,1221.1,'workspace_projection_end_month_annuity');
  pkg_workspace.save_goal(token_a,portfolio_a,goal_id,'Capital',2000,'USD',0,-50,NULL);
  project(1000,12,500,'workspace_projection_negative_expected_return');
  pkg_workspace.save_goal(token_b,portfolio_b,other_goal,'Foreign goal',100,'USD',0,0,NULL);
  reject_case(3,-20601,'workspace_goals_rejects_foreign_portfolio');
  reject_case(4,-20601,'workspace_archive_rejects_foreign_goal');
  reject_case(5,-20601,'workspace_projection_rejects_foreign_goal');
  reject_case(11,-20602,'workspace_rejects_horizon_above_limit');
  reject_case(12,-20602,'workspace_rejects_negative_horizon');
  reject_case(13,-20602,'workspace_rejects_fractional_horizon');
  reject_case(14,-20602,'workspace_rejects_negative_capital');
  reject_case(15,-20602,'workspace_rejects_minus_100_percent');
  reject_case(19,-20601,'workspace_save_goal_cannot_move_to_foreign_portfolio');
  reject_case(23,-20602,'workspace_rejects_target_rounded_to_zero');
  reject_case(24,-20602,'workspace_rejects_rate_rounded_to_minus_100');
  SELECT COUNT(*) INTO count_value FROM goals WHERE id=goal_id AND portfolio_id=portfolio_a AND title='Capital';
  check_true(count_value=1,'workspace_failed_calls_preserve_prior_uncommitted_goal');
  pkg_workspace.archive_goal(token_a,goal_id);
  reject_case(20,-20601,'workspace_archived_goal_cannot_be_projected');

  pkg_workspace.list_notifications(token_a,0,10,cursor_value);
  FETCH cursor_value INTO id_text,title_value,body_value,source_value,severity_value,timestamp_value;
  check_true(cursor_value%FOUND AND id_text=RAWTOHEX(notification_a) AND title_value='Own notification','workspace_notifications_owner_filter');
  FETCH cursor_value INTO id_text,title_value,body_value,source_value,severity_value,timestamp_value;
  check_true(cursor_value%NOTFOUND,'workspace_notifications_excludes_foreign_rows'); CLOSE cursor_value;
  reject_case(6,-20601,'workspace_cannot_dismiss_foreign_notification');
  reject_case(17,-20602,'workspace_notification_page_size_limit');
  pkg_workspace.dismiss_notification(token_a,notification_a);
  SELECT COUNT(*) INTO count_value FROM notifications WHERE id=notification_a AND deleted_at_utc IS NOT NULL;
  check_true(count_value=1,'workspace_notification_soft_delete');

  pkg_workspace.save_tag(token_a,'Long term',tag_a);
  pkg_workspace.save_tag(token_b,'Long term',tag_b);
  reject_case(16,-20603,'workspace_duplicate_tag_preserves_existing');
  reject_case(7,-20601,'workspace_cannot_rename_foreign_tag');
  reject_case(8,-20601,'workspace_cannot_tag_foreign_asset');
  reject_case(9,-20601,'workspace_cannot_use_foreign_tag');
  reject_case(10,-20601,'workspace_cannot_delete_foreign_tag');
  reject_case(22,-20602,'workspace_tag_enabled_is_explicit_boolean_number');
  pkg_workspace.list_tags(token_a,cursor_value);
  FETCH cursor_value INTO id_text,title_value;
  check_true(cursor_value%FOUND AND id_text=RAWTOHEX(tag_a),'workspace_tag_list_owner_filter');
  FETCH cursor_value INTO id_text,title_value;
  check_true(cursor_value%NOTFOUND,'workspace_tag_list_excludes_foreign'); CLOSE cursor_value;
  pkg_workspace.set_asset_tag(token_a,asset_a,tag_a,1);
  pkg_workspace.set_asset_tag(token_a,asset_a,tag_a,1);
  SELECT COUNT(*) INTO count_value FROM asset_tags WHERE asset_id=asset_a AND tag_id=tag_a;
  check_true(count_value=1,'workspace_tag_enable_is_idempotent');
  pkg_workspace.set_asset_tag(token_a,asset_a,tag_a,0);
  SELECT COUNT(*) INTO count_value FROM asset_tags WHERE asset_id=asset_a AND tag_id=tag_a;
  check_true(count_value=0,'workspace_tag_disable_removes_link');
  pkg_workspace.set_asset_tag(token_a,asset_a,tag_a,1);
  pkg_workspace.delete_tag(token_a,tag_a);
  SELECT COUNT(*) INTO count_value FROM asset_tags WHERE tag_id=tag_a;
  check_true(count_value=0,'workspace_delete_tag_removes_associations');
  SELECT COUNT(*) INTO count_value FROM tags WHERE id=tag_a;
  check_true(count_value=0,'workspace_delete_tag_removes_owned_tag');
  SELECT COUNT(*) INTO count_value FROM tags WHERE id=tag_b;
  check_true(count_value=1,'workspace_delete_does_not_touch_foreign_tag');

  SELECT COUNT(*) INTO count_value FROM audit_log WHERE user_id=user_a AND action IN ('SettingsSaved','GoalSaved','GoalArchived','TagSaved','TagDeleted','NotificationDismissed');
  check_true(count_value>=6,'workspace_changes_are_audited');
  SELECT COUNT(*) INTO count_value FROM audit_log WHERE user_id IN (user_a,user_b)
    AND (DBMS_LOB.INSTR(metadata_json,token_a)>0 OR DBMS_LOB.INSTR(metadata_json,'workspace-key-must-never-be-returned')>0);
  check_true(count_value=0,'workspace_audit_contains_no_token_or_api_secret');
  UPDATE users SET account_status='Blocked' WHERE id=user_a;
  reject_case(21,-20301,'workspace_blocked_user_loses_session_access');
  ROLLBACK TO workspace_fixtures;
  SELECT COUNT(*) INTO count_value FROM users WHERE id IN (user_a,user_b,admin_id);
  check_true(count_value=0,'workspace_api_never_commits_caller_transaction');
EXCEPTION WHEN OTHERS THEN ROLLBACK; RAISE;
END;
/
