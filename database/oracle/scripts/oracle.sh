#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
if [[ -f .env.oracle ]]; then
  while IFS='=' read -r key value; do
    case "$key" in ORACLE_IMAGE|ORACLE_PORT|ORACLE_ADMIN_PASSWORD|ORACLE_OWNER_PASSWORD|ORACLE_APP_PASSWORD|PROXIMA_ADMIN_LOGIN|PROXIMA_ADMIN_PASSWORD) export "$key=$value";; esac
  done < .env.oracle
fi
compose=(docker compose --env-file .env.oracle -f docker-compose.oracle.yml)
sql() { "${compose[@]}" exec -T -e NLS_LANG=AMERICAN_AMERICA.AL32UTF8 oracle sqlplus -s /nolog; }
validate_password() {
  [[ "$1" =~ ^[A-Za-z][A-Za-z0-9_#]{15,63}$ ]] || { echo 'Use 16-64 characters: leading letter, then letters/digits/_/#.' >&2; exit 1; }
}
case "${1:-}" in
 up) "${compose[@]}" up -d --wait ;;
 create-admin)
  : "${ORACLE_ADMIN_PASSWORD:?}" "${PROXIMA_ADMIN_LOGIN:?}" "${PROXIMA_ADMIN_PASSWORD:?}"
  validate_password "$ORACLE_ADMIN_PASSWORD"; validate_password "$PROXIMA_ADMIN_PASSWORD"
  [[ "$PROXIMA_ADMIN_LOGIN" =~ ^[a-z0-9_.-]{3,64}$ ]] || { echo 'Use a lowercase administrator login of 3-64 characters.' >&2; exit 1; }
  sql <<SQL
WHENEVER OSERROR EXIT FAILURE
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET ECHO OFF VERIFY OFF
CONNECT sys/"${ORACLE_ADMIN_PASSWORD}"@localhost:1521/FREEPDB1 AS SYSDBA
SET SERVEROUTPUT ON
DECLARE
  n PLS_INTEGER;
  administrator_id RAW(16);
BEGIN
  SELECT COUNT(*) INTO n FROM proxima_owner.users WHERE role='Admin' AND account_status='Active';
  IF n=0 THEN
    proxima_owner.pkg_auth.provision_admin('${PROXIMA_ADMIN_LOGIN}','${PROXIMA_ADMIN_PASSWORD}','Администратор',administrator_id);
    DBMS_OUTPUT.PUT_LINE('Initial application administrator created; password remains in local environment file.');
  ELSE
    DBMS_OUTPUT.PUT_LINE('An active administrator already exists; no account or password changed.');
  END IF;
END;
/
EXIT SUCCESS
SQL
  ;;
 auth)
  : "${ORACLE_ADMIN_PASSWORD:?}"; validate_password "$ORACLE_ADMIN_PASSWORD"
  sql <<SQL
WHENEVER OSERROR EXIT FAILURE
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CONNECT sys/"${ORACLE_ADMIN_PASSWORD}"@localhost:1521/FREEPDB1 AS SYSDBA
GRANT EXECUTE ON sys.dbms_crypto TO proxima_owner;
ALTER SESSION SET CURRENT_SCHEMA=proxima_owner;
@/opt/proxima/sql/15_auth_sessions.sql
@/opt/proxima/sql/16_integration_fields.sql
@/opt/proxima/sql/23_auth.sql
CREATE OR REPLACE CONTEXT proxima_ctx USING proxima_owner.pkg_auth;
@/opt/proxima/sql/40_ledger.sql
@/opt/proxima/sql/24_portfolios.sql
@/opt/proxima/sql/25_admin.sql
@/opt/proxima/sql/27_workspace.sql
@/opt/proxima/sql/39_csv.sql
@/opt/proxima/sql/26_api_grants.sql
DECLARE
  n PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO n FROM all_errors WHERE owner='PROXIMA_OWNER';
  IF n<>0 THEN RAISE_APPLICATION_ERROR(-20399,'Application API compilation failed'); END IF;
END;
/
EXIT SUCCESS
SQL
  ;;
 security)
  : "${ORACLE_ADMIN_PASSWORD:?}"; validate_password "$ORACLE_ADMIN_PASSWORD"
  sql <<SQL
WHENEVER OSERROR EXIT FAILURE
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CONNECT sys/"${ORACLE_ADMIN_PASSWORD}"@localhost:1521/FREEPDB1 AS SYSDBA
@/opt/proxima/sql/05_security_foundation.sql
@/opt/proxima/sql/06_security_accounts.sql
GRANT EXECUTE ON sys.dbms_crypto TO proxima_owner;
ALTER SESSION SET CURRENT_SCHEMA=proxima_owner;
@/opt/proxima/sql/21_password.sql
DECLARE
  n PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO n FROM all_errors WHERE owner='PROXIMA_OWNER' AND name='PKG_PASSWORD';
  IF n<>0 THEN RAISE_APPLICATION_ERROR(-20299,'PKG_PASSWORD compilation failed'); END IF;
END;
/
@/opt/proxima/sql/98_password_tests.sql
@/opt/proxima/sql/99_security_foundation_tests.sql
EXIT SUCCESS
SQL
  ;;
 bootstrap)
  : "${ORACLE_ADMIN_PASSWORD:?}" "${ORACLE_OWNER_PASSWORD:?}" "${ORACLE_APP_PASSWORD:?}"
  validate_password "$ORACLE_ADMIN_PASSWORD"; validate_password "$ORACLE_OWNER_PASSWORD"; validate_password "$ORACLE_APP_PASSWORD"
  if [[ "$ORACLE_ADMIN_PASSWORD" == "$ORACLE_OWNER_PASSWORD" || "$ORACLE_ADMIN_PASSWORD" == "$ORACLE_APP_PASSWORD" || "$ORACLE_OWNER_PASSWORD" == "$ORACLE_APP_PASSWORD" ]]; then
    echo 'Admin, owner and app passwords must be distinct.' >&2; exit 1
  fi
  sql <<SQL
WHENEVER OSERROR EXIT FAILURE
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CONNECT sys/"${ORACLE_ADMIN_PASSWORD}"@localhost:1521/FREEPDB1 AS SYSDBA
@/opt/proxima/sql/05_security_foundation.sql
CREATE USER proxima_owner IDENTIFIED BY "${ORACLE_OWNER_PASSWORD}" DEFAULT TABLESPACE users QUOTA 200M ON users PROFILE proxima_owner_profile;
GRANT CREATE SESSION, CREATE TABLE, CREATE PROCEDURE TO proxima_owner;
GRANT EXECUTE ON sys.dbms_crypto TO proxima_owner;
CREATE USER proxima_app IDENTIFIED BY "${ORACLE_APP_PASSWORD}" PROFILE proxima_runtime_profile;
CONNECT proxima_owner/"${ORACLE_OWNER_PASSWORD}"@localhost:1521/FREEPDB1
@/opt/proxima/sql/10_schema.sql
@/opt/proxima/sql/15_auth_sessions.sql
@/opt/proxima/sql/20_api.sql
@/opt/proxima/sql/21_password.sql
@/opt/proxima/sql/23_auth.sql
@/opt/proxima/sql/40_ledger.sql
@/opt/proxima/sql/24_portfolios.sql
@/opt/proxima/sql/25_admin.sql
@/opt/proxima/sql/27_workspace.sql
@/opt/proxima/sql/30_seed.sql
@/opt/proxima/sql/90_verify.sql
CONNECT sys/"${ORACLE_ADMIN_PASSWORD}"@localhost:1521/FREEPDB1 AS SYSDBA
CREATE OR REPLACE CONTEXT proxima_ctx USING proxima_owner.pkg_auth;
@/opt/proxima/sql/06_security_accounts.sql
@/opt/proxima/sql/26_api_grants.sql
EXIT SUCCESS
SQL
  ;;
 ledger|ledger-test|ledger-show)
  : "${ORACLE_ADMIN_PASSWORD:?}"; validate_password "$ORACLE_ADMIN_PASSWORD"
  case "$1" in
    ledger) ledger_file=40_ledger.sql ;;
    ledger-test) ledger_file=96_ledger_tests.sql ;;
    ledger-show) ledger_file=97_ledger_walkthrough.sql ;;
  esac
  sql <<SQL
WHENEVER OSERROR EXIT FAILURE
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CONNECT sys/"${ORACLE_ADMIN_PASSWORD}"@localhost:1521/FREEPDB1 AS SYSDBA
ALTER SESSION SET CURRENT_SCHEMA=proxima_owner;
@/opt/proxima/sql/${ledger_file}
DECLARE
  n PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO n FROM all_errors WHERE owner='PROXIMA_OWNER' AND name='PKG_LEDGER';
  IF n <> 0 THEN RAISE_APPLICATION_ERROR(-20099,'PKG_LEDGER has compilation errors'); END IF;
END;
/
EXIT SUCCESS
SQL
  ;;
 smoke)
  : "${ORACLE_APP_PASSWORD:?}"; validate_password "$ORACLE_APP_PASSWORD"
  sql <<SQL
WHENEVER OSERROR EXIT FAILURE
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CONNECT proxima_app/"${ORACLE_APP_PASSWORD}"@localhost:1521/FREEPDB1
@/opt/proxima/sql/95_smoke.sql
EXIT SUCCESS
SQL
  ;;
 *) echo "Usage: $0 {up|bootstrap|security|auth|create-admin|smoke|ledger|ledger-test|ledger-show}" >&2; exit 2 ;;
esac
