#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
if [[ -f .env.oracle ]]; then
  while IFS='=' read -r key value; do
    case "$key" in ORACLE_IMAGE|ORACLE_PORT|ORACLE_ADMIN_PASSWORD|ORACLE_OWNER_PASSWORD|ORACLE_APP_PASSWORD) export "$key=$value";; esac
  done < .env.oracle
fi
compose=(docker compose --env-file .env.oracle -f docker-compose.oracle.yml)
sql() { "${compose[@]}" exec -T -e NLS_LANG=AMERICAN_AMERICA.AL32UTF8 oracle sqlplus -s /nolog; }
validate_password() {
  [[ "$1" =~ ^[A-Za-z][A-Za-z0-9_#]{11,63}$ ]] || { echo 'Use 12-64 characters: leading letter, then letters/digits/_/#.' >&2; exit 1; }
}
case "${1:-}" in
 up) "${compose[@]}" up -d --wait ;;
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
CREATE USER proxima_owner IDENTIFIED BY "${ORACLE_OWNER_PASSWORD}" DEFAULT TABLESPACE users QUOTA 200M ON users;
GRANT CREATE SESSION, CREATE TABLE, CREATE PROCEDURE TO proxima_owner;
CREATE USER proxima_app IDENTIFIED BY "${ORACLE_APP_PASSWORD}";
GRANT CREATE SESSION TO proxima_app;
CONNECT proxima_owner/"${ORACLE_OWNER_PASSWORD}"@localhost:1521/FREEPDB1
@/opt/proxima/sql/10_schema.sql
@/opt/proxima/sql/20_api.sql
@/opt/proxima/sql/30_seed.sql
@/opt/proxima/sql/90_verify.sql
GRANT EXECUTE ON pkg_reference TO proxima_app;
CONNECT sys/"${ORACLE_ADMIN_PASSWORD}"@localhost:1521/FREEPDB1 AS SYSDBA
ALTER USER proxima_owner ACCOUNT LOCK;
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
 *) echo "Usage: $0 {up|bootstrap|smoke}" >&2; exit 2 ;;
esac
