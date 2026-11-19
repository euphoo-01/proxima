-- SYS внутри прикладной PDB; только проверки, без DDL и изменения паролей.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
ALTER SESSION SET NLS_NUMERIC_CHARACTERS='.,';
DECLARE
  n NUMBER; accepted BOOLEAN;
  PROCEDURE require(p_ok BOOLEAN,p_case VARCHAR2) IS
  BEGIN
    IF p_ok IS NULL OR NOT p_ok THEN RAISE_APPLICATION_ERROR(-20991,'Security assertion failed: '||p_case); END IF;
  END;
  PROCEDURE reject_password(p_password VARCHAR2,p_case VARCHAR2,p_old VARCHAR2 DEFAULT NULL,p_code NUMBER DEFAULT -20201) IS
    code NUMBER:=0; result BOOLEAN;
  BEGIN
    BEGIN result:=sys.proxima_password_verify('PROXIMA_APP',p_password,p_old);
    EXCEPTION WHEN OTHERS THEN code:=SQLCODE;
    END;
    require(code=p_code,p_case);
  END;
  PROCEDURE numeric_limit(p_profile VARCHAR2,p_resource VARCHAR2,p_expected NUMBER) IS
    value VARCHAR2(128);
  BEGIN
    SELECT limit INTO value FROM dba_profiles WHERE profile=p_profile AND resource_name=p_resource;
    require(value NOT IN ('DEFAULT','UNLIMITED'),p_profile||'_'||p_resource||'_explicit');
    IF p_resource='PASSWORD_LOCK_TIME' THEN
      -- DBA_PROFILES отображает дробное число суток с четырьмя знаками.
      require(TO_NUMBER(value)=TRUNC(p_expected,4),p_profile||'_'||p_resource);
    ELSE
      require(ABS(TO_NUMBER(value)-p_expected)<0.000000001,p_profile||'_'||p_resource);
    END IF;
  END;
  PROCEDURE text_limit(p_profile VARCHAR2,p_resource VARCHAR2,p_expected VARCHAR2) IS
    value VARCHAR2(128);
  BEGIN
    SELECT limit INTO value FROM dba_profiles WHERE profile=p_profile AND resource_name=p_resource;
    IF p_resource='PASSWORD_VERIFY_FUNCTION' THEN
      -- Oracle хранит имя SYS-функции в профиле без квалификатора схемы.
      require(REPLACE(UPPER(value),'"','') IN ('PROXIMA_PASSWORD_VERIFY','SYS.PROXIMA_PASSWORD_VERIFY'),p_profile||'_'||p_resource);
    ELSE
      require(REPLACE(UPPER(value),'"','')=p_expected,p_profile||'_'||p_resource);
    END IF;
  END;
BEGIN
  require(SYS_CONTEXT('USERENV','SESSION_USER')='SYS' AND SYS_CONTEXT('USERENV','CON_NAME')<>'CDB$ROOT','sys_inside_pdb');
  accepted:=sys.proxima_password_verify('PROXIMA_APP','GoodPassword1234',NULL);
  require(accepted,'password_accepts_16_characters');
  accepted:=sys.proxima_password_verify('PROXIMA_APP','Aa1'||RPAD('x',61,'x'),NULL);
  require(accepted,'password_accepts_64_characters');
  accepted:=sys.proxima_password_verify('PROXIMA_APP','GoodPassword1234','DifferentPassword5678');
  require(accepted,'password_accepts_different_previous_value');
  reject_password(NULL,'password_rejects_null');
  reject_password('','password_rejects_empty');
  reject_password('GoodPassword123','password_rejects_15_characters');
  reject_password('Aa1'||RPAD('x',62,'x'),'password_rejects_65_characters');
  reject_password('ABCDEFGHIJKLMNOP1','password_requires_lowercase');
  reject_password('abcdefghijklmnop1','password_requires_uppercase');
  reject_password('Abcdefghijklmnopq','password_requires_digit');
  reject_password('Someproxima_app9Abc','password_rejects_username_case_insensitively');
  reject_password('GoodPassword1234','password_rejects_equal_old','GoodPassword1234',-20202);
  DBMS_OUTPUT.PUT_LINE('PASS password_boundaries_composition_and_reuse');

  SELECT COUNT(*) INTO n FROM dba_objects WHERE owner='SYS' AND object_name='PROXIMA_PASSWORD_VERIFY'
    AND object_type='FUNCTION' AND status='VALID';
  require(n=1,'profile_verify_function_is_valid_sys_owned_function');
  FOR p IN (SELECT 'PROXIMA_OWNER_PROFILE' name FROM dual UNION ALL SELECT 'PROXIMA_RUNTIME_PROFILE' FROM dual) LOOP
    numeric_limit(p.name,'PASSWORD_GRACE_TIME',7);
    numeric_limit(p.name,'PASSWORD_REUSE_TIME',365);
    numeric_limit(p.name,'PASSWORD_REUSE_MAX',10);
    text_limit(p.name,'PASSWORD_VERIFY_FUNCTION','SYS.PROXIMA_PASSWORD_VERIFY');
  END LOOP;
  numeric_limit('PROXIMA_OWNER_PROFILE','FAILED_LOGIN_ATTEMPTS',3);
  numeric_limit('PROXIMA_OWNER_PROFILE','PASSWORD_LOCK_TIME',1);
  numeric_limit('PROXIMA_OWNER_PROFILE','PASSWORD_LIFE_TIME',180);
  numeric_limit('PROXIMA_OWNER_PROFILE','SESSIONS_PER_USER',2);
  numeric_limit('PROXIMA_OWNER_PROFILE','IDLE_TIME',15);
  numeric_limit('PROXIMA_OWNER_PROFILE','CONNECT_TIME',120);
  numeric_limit('PROXIMA_RUNTIME_PROFILE','FAILED_LOGIN_ATTEMPTS',5);
  numeric_limit('PROXIMA_RUNTIME_PROFILE','PASSWORD_LOCK_TIME',1/24);
  text_limit('PROXIMA_RUNTIME_PROFILE','PASSWORD_LIFE_TIME','UNLIMITED');
  numeric_limit('PROXIMA_RUNTIME_PROFILE','SESSIONS_PER_USER',64);
  numeric_limit('PROXIMA_RUNTIME_PROFILE','IDLE_TIME',30);
  numeric_limit('PROXIMA_RUNTIME_PROFILE','CONNECT_TIME',480);
  SELECT COUNT(*) INTO n FROM v$parameter WHERE name='resource_limit' AND UPPER(value)='TRUE';
  require(n=1,'profile_resource_limits_enforced');
  SELECT COUNT(*) INTO n FROM dba_users WHERE username='PROXIMA_OWNER'
    AND profile='PROXIMA_OWNER_PROFILE' AND INSTR(account_status,'LOCKED')>0;
  require(n=1,'owner_profile_and_locked_account');
  SELECT COUNT(*) INTO n FROM dba_users WHERE username='PROXIMA_APP' AND profile='PROXIMA_RUNTIME_PROFILE' AND account_status='OPEN';
  require(n=1,'runtime_profile_and_open_account');
  DBMS_OUTPUT.PUT_LINE('PASS effective_profiles_owner_lock_and_resource_limit');

  SELECT COUNT(*) INTO n FROM dba_role_privs WHERE grantee='PROXIMA_APP';
  require(n=2,'runtime_exactly_two_roles');
  SELECT COUNT(*) INTO n FROM dba_role_privs WHERE grantee='PROXIMA_APP'
    AND granted_role IN ('PROXIMA_CONNECT_ROLE','PROXIMA_API_ROLE') AND default_role='YES' AND admin_option='NO';
  require(n=2,'runtime_default_roles_without_admin_option');
  SELECT COUNT(*) INTO n FROM dba_role_privs WHERE grantee IN ('PROXIMA_CONNECT_ROLE','PROXIMA_API_ROLE');
  require(n=0,'service_roles_have_no_nested_roles');
  SELECT COUNT(*) INTO n FROM dba_sys_privs WHERE grantee='PROXIMA_APP';
  require(n=0,'runtime_has_no_direct_system_privileges');
  SELECT COUNT(*) INTO n FROM dba_sys_privs WHERE grantee='PROXIMA_CONNECT_ROLE';
  require(n=1,'connect_role_exactly_one_system_privilege');
  SELECT COUNT(*) INTO n FROM dba_sys_privs WHERE grantee='PROXIMA_CONNECT_ROLE' AND privilege='CREATE SESSION' AND admin_option='NO';
  require(n=1,'runtime_connects_through_role');
  SELECT COUNT(*) INTO n FROM dba_sys_privs WHERE grantee='PROXIMA_API_ROLE';
  require(n=0,'api_role_has_no_system_privileges');
  SELECT COUNT(*) INTO n FROM v$pwfile_users WHERE username='PROXIMA_APP'
    AND (sysdba='TRUE' OR sysoper='TRUE' OR sysbackup='TRUE' OR sysdg='TRUE' OR syskm='TRUE');
  require(n=0,'runtime_has_no_password_file_administration');
  SELECT COUNT(*) INTO n FROM dba_sys_privs WHERE INSTR(privilege,' ANY ')>0
    AND (grantee IN ('PROXIMA_APP','PUBLIC') OR grantee IN
      (SELECT granted_role FROM dba_role_privs START WITH grantee IN ('PROXIMA_APP','PUBLIC') CONNECT BY NOCYCLE PRIOR granted_role=grantee));
  require(n=0,'runtime_and_public_have_no_any_privilege_bypass');
  DBMS_OUTPUT.PUT_LINE('PASS least_privilege_runtime_role_graph');

  SELECT COUNT(*) INTO n FROM dba_tab_privs WHERE grantee='PROXIMA_APP';
  require(n=0,'runtime_has_no_direct_object_grants');
  SELECT COUNT(*) INTO n FROM dba_tab_privs WHERE grantee='PROXIMA_CONNECT_ROLE';
  require(n=0,'connect_role_has_no_object_grants');
  SELECT COUNT(*) INTO n FROM dba_tab_privs WHERE grantee='PROXIMA_API_ROLE';
  require(n BETWEEN 1 AND 4,'api_role_public_package_grant_count');
  SELECT COUNT(*) INTO n FROM dba_tab_privs WHERE grantee='PROXIMA_API_ROLE'
    AND NOT (owner='PROXIMA_OWNER' AND privilege='EXECUTE' AND grantable='NO'
      AND table_name IN ('PKG_REFERENCE','PKG_AUTH','PKG_PORTFOLIOS','PKG_ADMIN','PKG_WORKSPACE','PKG_CSV','PKG_MARKET','PKG_ANALYTICS'));
  require(n=0,'api_role_only_approved_package_execute_grants');
  SELECT COUNT(*) INTO n FROM dba_tab_privs WHERE grantee='PROXIMA_API_ROLE' AND owner='PROXIMA_OWNER'
    AND table_name='PKG_REFERENCE' AND privilege='EXECUTE' AND grantable='NO';
  require(n=1,'api_role_requires_reference_package_execute');
  SELECT COUNT(*) INTO n FROM dba_tab_privs p JOIN dba_objects o
    ON o.owner=p.owner AND o.object_name=p.table_name AND o.object_type IN ('TABLE','VIEW','MATERIALIZED VIEW')
    WHERE p.owner='PROXIMA_OWNER' AND (p.grantee IN ('PROXIMA_APP','PUBLIC') OR p.grantee IN
      (SELECT granted_role FROM dba_role_privs START WITH grantee IN ('PROXIMA_APP','PUBLIC') CONNECT BY NOCYCLE PRIOR granted_role=grantee));
  require(n=0,'project_tables_have_no_runtime_public_or_role_grants');
  SELECT COUNT(*) INTO n FROM dba_col_privs WHERE owner='PROXIMA_OWNER'
    AND (grantee IN ('PROXIMA_APP','PUBLIC') OR grantee IN
      (SELECT granted_role FROM dba_role_privs START WITH grantee IN ('PROXIMA_APP','PUBLIC') CONNECT BY NOCYCLE PRIOR granted_role=grantee));
  require(n=0,'project_columns_have_no_runtime_public_or_role_grants');
  SELECT COUNT(*) INTO n FROM dba_tab_privs WHERE owner='PROXIMA_OWNER' AND privilege='EXECUTE'
    AND table_name NOT IN ('PKG_REFERENCE','PKG_AUTH','PKG_PORTFOLIOS','PKG_ADMIN','PKG_WORKSPACE','PKG_CSV','PKG_MARKET','PKG_ANALYTICS') AND (grantee IN ('PROXIMA_APP','PUBLIC') OR grantee IN
      (SELECT granted_role FROM dba_role_privs START WITH grantee IN ('PROXIMA_APP','PUBLIC') CONNECT BY NOCYCLE PRIOR granted_role=grantee));
  require(n=0,'internal_packages_are_not_public_apis');
  DBMS_OUTPUT.PUT_LINE('PASS project_data_and_internal_packages_are_private');

  SELECT COUNT(DISTINCT audit_option) INTO n FROM audit_unified_policies
    WHERE policy_name='PROXIMA_CONNECTION_POLICY' AND audit_option IN ('LOGON','LOGOFF');
  require(n=2,'connection_audit_defines_logon_and_logoff');
  SELECT COUNT(*) INTO n FROM audit_unified_enabled_policies WHERE policy_name='PROXIMA_CONNECTION_POLICY'
    AND enabled_option='BY USER' AND entity_name='PROXIMA_APP' AND entity_type='USER' AND success='YES' AND failure='YES';
  require(n=1,'connection_audit_enabled_for_runtime_success_and_failure');
  DBMS_OUTPUT.PUT_LINE('PASS runtime_logon_logoff_audit_policy_enabled');
  DBMS_OUTPUT.PUT_LINE('Security foundation assertions passed.');
END;
/
