WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_auth AUTHID DEFINER AS
  PROCEDURE register_user(p_login VARCHAR2,p_password VARCHAR2,p_display_name VARCHAR2,p_user_id OUT RAW);
  PROCEDURE login(p_login VARCHAR2,p_password VARCHAR2,p_token OUT VARCHAR2,p_user_id OUT RAW,p_role OUT VARCHAR2);
  PROCEDURE logout(p_token VARCHAR2);
  FUNCTION require_user(p_token VARCHAR2) RETURN RAW;
  FUNCTION require_admin(p_token VARCHAR2) RETURN RAW;
  FUNCTION user_role(p_token VARCHAR2) RETURN VARCHAR2;
  PROCEDURE begin_request(p_token VARCHAR2);
  FUNCTION current_user_id RETURN RAW;
  PROCEDURE provision_admin(p_login VARCHAR2,p_password VARCHAR2,p_display_name VARCHAR2,p_user_id OUT RAW);
END pkg_auth;
/
CREATE OR REPLACE PACKAGE BODY pkg_auth AS
  FUNCTION normalized_login(p_login VARCHAR2) RETURN VARCHAR2 IS
    result VARCHAR2(32767):=LOWER(TRIM(p_login));
  BEGIN
    IF result IS NULL OR LENGTH(result)<3 OR LENGTH(result)>64
      OR NOT REGEXP_LIKE(result,'^[a-z0-9_.-]+$','c') THEN
      RAISE_APPLICATION_ERROR(-20300,'Invalid registration details');
    END IF;
    RETURN result;
  END;
  FUNCTION token_digest(p_token VARCHAR2) RETURN RAW IS
  BEGIN
    IF p_token IS NULL OR LENGTH(p_token)<>64 OR NOT REGEXP_LIKE(p_token,'^[0-9A-Fa-f]{64}$','c') THEN
      RETURN NULL;
    END IF;
    RETURN DBMS_CRYPTO.HASH(HEXTORAW(p_token),DBMS_CRYPTO.HASH_SH256);
  END;
  PROCEDURE audit_event(p_user RAW,p_action VARCHAR2) IS
  BEGIN
    INSERT INTO audit_log(user_id,action,timestamp,metadata_json)
      VALUES(p_user,p_action,SYSTIMESTAMP,JSON_OBJECT('database_session_user' VALUE SYS_CONTEXT('USERENV','SESSION_USER') RETURNING CLOB));
  END;
  PROCEDURE create_account(p_login VARCHAR2,p_password VARCHAR2,p_display_name VARCHAR2,p_role VARCHAR2,p_user_id OUT RAW) IS
    login_value VARCHAR2(64); password_value VARCHAR2(512);
  BEGIN
    login_value:=normalized_login(p_login);
    IF TRIM(p_display_name) IS NULL OR LENGTH(p_display_name)>255 THEN RAISE_APPLICATION_ERROR(-20300,'Invalid registration details'); END IF;
    password_value:=pkg_password.hash_password(p_password);
    p_user_id:=SYS_GUID();
    INSERT INTO users(id,login,display_name,role,password_hash,failed_unlock_attempts,account_status)
      VALUES(p_user_id,login_value,TRIM(p_display_name),p_role,password_value,0,'Active');
    audit_event(p_user_id,'AccountRegistered');
  END;
  PROCEDURE register_user(p_login VARCHAR2,p_password VARCHAR2,p_display_name VARCHAR2,p_user_id OUT RAW) IS
    PRAGMA AUTONOMOUS_TRANSACTION;
  BEGIN
    p_user_id:=NULL;
    create_account(p_login,p_password,p_display_name,'PrivateInvestor',p_user_id);
    COMMIT;
  EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN ROLLBACK; p_user_id:=NULL; RAISE_APPLICATION_ERROR(-20300,'Invalid registration details');
    WHEN OTHERS THEN ROLLBACK; p_user_id:=NULL; RAISE;
  END;
  PROCEDURE provision_admin(p_login VARCHAR2,p_password VARCHAR2,p_display_name VARCHAR2,p_user_id OUT RAW) IS
    PRAGMA AUTONOMOUS_TRANSACTION;
  BEGIN
    p_user_id:=NULL;
    IF SYS_CONTEXT('USERENV','SESSION_USER') NOT IN ('SYS','PROXIMA_OWNER') THEN
      RAISE_APPLICATION_ERROR(-20302,'Administrative provisioning requires trusted database session');
    END IF;
    create_account(p_login,p_password,p_display_name,'Admin',p_user_id);
    COMMIT;
  EXCEPTION WHEN OTHERS THEN ROLLBACK; p_user_id:=NULL; RAISE;
  END;
  PROCEDURE dummy_verify(p_password VARCHAR2) IS
    result RAW(32); candidate VARCHAR2(128):='invalid password placeholder';
  BEGIN
    IF p_password IS NOT NULL AND LENGTH(p_password) BETWEEN 12 AND 128 THEN candidate:=p_password; END IF;
    result:=pkg_password.derive_key(candidate,HEXTORAW('00000000000000000000000000000000'),600000);
  END;
  PROCEDURE login(p_login VARCHAR2,p_password VARCHAR2,p_token OUT VARCHAR2,p_user_id OUT RAW,p_role OUT VARCHAR2) IS
    PRAGMA AUTONOMOUS_TRANSACTION;
    id_value RAW(16); initial_hash VARCHAR2(512); locked_hash VARCHAR2(512);
    status_value VARCHAR2(255); role_value VARCHAR2(255); valid_password BOOLEAN:=FALSE;
    attempts_value NUMBER; token_value VARCHAR2(64); hash_value RAW(32);
  BEGIN
    p_token:=NULL; p_user_id:=NULL; p_role:=NULL;
    DBMS_SESSION.SET_CONTEXT('PROXIMA_CTX','TOKEN_HASH',NULL);
    BEGIN
      SELECT id,password_hash INTO id_value,initial_hash FROM users WHERE login=LOWER(TRIM(p_login));
    EXCEPTION WHEN NO_DATA_FOUND THEN
      dummy_verify(p_password);
      audit_event(NULL,'LoginFailed'); COMMIT;
      RAISE_APPLICATION_ERROR(-20301,'Login failed');
    END;
    IF p_password IS NULL OR LENGTH(p_password)<12 OR LENGTH(p_password)>128 THEN
      dummy_verify(p_password);
    ELSE
      valid_password:=pkg_password.verify_password(p_password,initial_hash);
    END IF;
    SELECT password_hash,account_status,role INTO locked_hash,status_value,role_value
      FROM users WHERE id=id_value FOR UPDATE;
    IF locked_hash<>initial_hash AND p_password IS NOT NULL AND LENGTH(p_password) BETWEEN 12 AND 128 THEN
      valid_password:=pkg_password.verify_password(p_password,locked_hash);
    END IF;
    IF NOT valid_password OR status_value<>'Active' OR role_value NOT IN ('Admin','PrivateInvestor','FinancialConsultant') THEN
      UPDATE users SET failed_unlock_attempts=LEAST(failed_unlock_attempts+1,5),
        account_status=CASE WHEN failed_unlock_attempts+1>=5 THEN 'Blocked' ELSE account_status END,
        updated_at=SYSTIMESTAMP WHERE id=id_value RETURNING failed_unlock_attempts INTO attempts_value;
      audit_event(id_value,'LoginFailed'); COMMIT;
      RAISE_APPLICATION_ERROR(-20301,'Login failed');
    END IF;
    token_value:=RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(32)); hash_value:=token_digest(token_value);
    UPDATE users SET failed_unlock_attempts=0,updated_at=SYSTIMESTAMP WHERE id=id_value;
    INSERT INTO auth_sessions(id,user_id,token_hash,created_at,expires_at)
      VALUES(SYS_GUID(),id_value,hash_value,SYSTIMESTAMP,SYSTIMESTAMP+INTERVAL '8' HOUR);
    audit_event(id_value,'LoginSucceeded');
    COMMIT;
    p_token:=token_value; p_user_id:=id_value; p_role:=role_value;
  EXCEPTION WHEN OTHERS THEN ROLLBACK; p_token:=NULL; p_user_id:=NULL; p_role:=NULL; RAISE;
  END;
  FUNCTION resolve_hash(p_hash RAW) RETURN RAW IS
    result RAW(16);
  BEGIN
    IF p_hash IS NULL THEN RAISE_APPLICATION_ERROR(-20301,'Authentication required'); END IF;
    SELECT u.id INTO result FROM auth_sessions s JOIN users u ON u.id=s.user_id
      WHERE s.token_hash=p_hash AND s.revoked_at IS NULL AND s.expires_at>SYSTIMESTAMP
        AND u.account_status='Active' AND u.role IN ('Admin','PrivateInvestor','FinancialConsultant');
    RETURN result;
  EXCEPTION WHEN NO_DATA_FOUND THEN RAISE_APPLICATION_ERROR(-20301,'Authentication required');
  END;
  FUNCTION require_user(p_token VARCHAR2) RETURN RAW IS
  BEGIN
    RETURN resolve_hash(token_digest(p_token));
  END;
  FUNCTION user_role(p_token VARCHAR2) RETURN VARCHAR2 IS
    id_value RAW(16); result VARCHAR2(255);
  BEGIN
    id_value:=require_user(p_token);
    SELECT role INTO result FROM users WHERE id=id_value;
    RETURN result;
  END;
  FUNCTION require_admin(p_token VARCHAR2) RETURN RAW IS
    result RAW(16);
  BEGIN
    result:=require_user(p_token);
    IF user_role(p_token)<>'Admin' THEN RAISE_APPLICATION_ERROR(-20302,'Administrator role required'); END IF;
    RETURN result;
  END;
  PROCEDURE begin_request(p_token VARCHAR2) IS
    id_value RAW(16);
  BEGIN
    DBMS_SESSION.SET_CONTEXT('PROXIMA_CTX','TOKEN_HASH',NULL);
    id_value:=require_user(p_token);
    DBMS_SESSION.SET_CONTEXT('PROXIMA_CTX','TOKEN_HASH',RAWTOHEX(token_digest(p_token)));
  END;
  FUNCTION current_user_id RETURN RAW IS
    hash_text VARCHAR2(64):=SYS_CONTEXT('PROXIMA_CTX','TOKEN_HASH');
  BEGIN
    IF hash_text IS NULL OR LENGTH(hash_text)<>64 OR NOT REGEXP_LIKE(hash_text,'^[0-9A-F]{64}$','c') THEN
      RAISE_APPLICATION_ERROR(-20301,'Authentication required');
    END IF;
    RETURN resolve_hash(HEXTORAW(hash_text));
  END;
  PROCEDURE logout(p_token VARCHAR2) IS
    PRAGMA AUTONOMOUS_TRANSACTION;
    hash_value RAW(32); id_value RAW(16);
  BEGIN
    DBMS_SESSION.SET_CONTEXT('PROXIMA_CTX','TOKEN_HASH',NULL);
    hash_value:=token_digest(p_token);
    IF hash_value IS NOT NULL THEN
      BEGIN
        SELECT user_id INTO id_value FROM auth_sessions WHERE token_hash=hash_value FOR UPDATE;
        UPDATE auth_sessions SET revoked_at=SYSTIMESTAMP WHERE token_hash=hash_value AND revoked_at IS NULL;
        IF SQL%ROWCOUNT=1 THEN audit_event(id_value,'SessionRevoked'); END IF;
      EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
      END;
    END IF;
    COMMIT;
  EXCEPTION WHEN OTHERS THEN ROLLBACK; RAISE;
  END;
END pkg_auth;
/
