WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
ALTER USER proxima_owner PROFILE proxima_owner_profile ACCOUNT LOCK;
ALTER USER proxima_app PROFILE proxima_runtime_profile;
GRANT proxima_connect_role,proxima_api_role TO proxima_app;
ALTER USER proxima_app DEFAULT ROLE proxima_connect_role,proxima_api_role;
GRANT EXECUTE ON proxima_owner.pkg_reference TO proxima_api_role;
DECLARE
  n PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO n FROM dba_sys_privs
    WHERE grantee='PROXIMA_APP' AND privilege='CREATE SESSION';
  IF n>0 THEN EXECUTE IMMEDIATE 'REVOKE CREATE SESSION FROM proxima_app'; END IF;
  SELECT COUNT(*) INTO n FROM dba_tab_privs WHERE grantee='PROXIMA_APP'
    AND owner='PROXIMA_OWNER' AND table_name='PKG_REFERENCE' AND privilege='EXECUTE';
  IF n>0 THEN EXECUTE IMMEDIATE 'REVOKE EXECUTE ON proxima_owner.pkg_reference FROM proxima_app'; END IF;
  SELECT COUNT(*) INTO n FROM audit_unified_policies WHERE policy_name='PROXIMA_CONNECTION_POLICY';
  IF n=0 THEN
    EXECUTE IMMEDIATE 'CREATE AUDIT POLICY proxima_connection_policy ACTIONS LOGON, LOGOFF';
  END IF;
END;
/
AUDIT POLICY proxima_connection_policy BY proxima_app;
