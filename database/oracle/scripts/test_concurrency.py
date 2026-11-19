#!/usr/bin/env python3
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import re
import subprocess
import uuid

root = Path(__file__).resolve().parents[3]
owner = uuid.uuid4().hex.upper()
portfolio = None


def sql(text, must_succeed=True):
    prefix = ('WHENEVER OSERROR EXIT FAILURE\nWHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK\n'
              'ALTER SESSION SET CONTAINER=FREEPDB1;\nALTER SESSION SET CURRENT_SCHEMA=PROXIMA_OWNER;\n'
              'SET SERVEROUTPUT ON\nSET FEEDBACK OFF\nSET HEADING OFF\nSET PAGESIZE 0\n')
    result = subprocess.run(['docker', 'compose', '--env-file', '.env.oracle', '-f', 'docker-compose.oracle.yml',
                             'exec', '-T', 'oracle', 'sqlplus', '-s', '/', 'as', 'sysdba'],
                            input=prefix + text + '\nEXIT;\n', text=True, capture_output=True, cwd=root, timeout=30)
    if must_succeed and result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    return result


try:
    setup = sql(f"""
DECLARE p RAW(16); t RAW(16);
BEGIN
  INSERT INTO users(id,login,display_name,role,password_hash,account_status)
    VALUES(HEXTORAW('{owner}'),'race-{owner}','Concurrent calls','PrivateInvestor','not-a-login-credential','Active');
  pkg_ledger.create_portfolio(HEXTORAW('{owner}'),'Concurrent calls','USD',p);
  pkg_ledger.post_cash(HEXTORAW('{owner}'),p,'Deposit',TIMESTAMP '2026-01-01 12:00:00 UTC',10000,'USD',0,t);
  pkg_ledger.post_trade(HEXTORAW('{owner}'),p,HEXTORAW('00000000000000000000000000000001'),
    'Buy',TIMESTAMP '2026-01-02 12:00:00 UTC',10,100,0,t);
  DBMS_OUTPUT.PUT_LINE('PORTFOLIO:'||RAWTOHEX(p));
  COMMIT;
END;
/
""")
    match = re.search(r'PORTFOLIO:([0-9A-F]{32})', setup.stdout)
    if match is None:
        raise RuntimeError('Fixture identifier not returned: ' + setup.stdout + setup.stderr)
    portfolio = match.group(1)

    def sell(day, delay):
        return sql(f"""
DECLARE t RAW(16);
BEGIN
  pkg_ledger.post_trade(HEXTORAW('{owner}'),HEXTORAW('{portfolio}'),
    HEXTORAW('00000000000000000000000000000001'),'Sell',
    TIMESTAMP '2026-01-0{day} 12:00:00 UTC',7,150,0,t);
  DBMS_SESSION.SLEEP({delay});
  COMMIT;
END;
/
""", must_succeed=False)

    with ThreadPoolExecutor(max_workers=2) as pool:
        first = pool.submit(sell, 3, 2)
        second = pool.submit(sell, 4, 0)
        results = [first.result(), second.result()]
    if sum(r.returncode == 0 for r in results) != 1:
        raise RuntimeError('Exactly one concurrent sale must succeed: ' + '\n'.join(r.stdout for r in results))
    failed = next(r for r in results if r.returncode)
    if 'ORA-20108' not in failed.stdout:
        raise RuntimeError('Failure must be insufficient instrument balance: ' + failed.stdout)
    verified = sql(f"""
DECLARE n NUMBER;
BEGIN
  SELECT quantity INTO n FROM assets WHERE portfolio_id=HEXTORAW('{portfolio}');
  IF n<>3 THEN RAISE_APPLICATION_ERROR(-20990,'Expected three remaining instruments'); END IF;
  SELECT COUNT(*) INTO n FROM transactions WHERE portfolio_id=HEXTORAW('{portfolio}') AND type='Sell';
  IF n<>1 THEN RAISE_APPLICATION_ERROR(-20991,'Expected exactly one committed sale'); END IF;
END;
/
""")
    print('PASS concurrent_sales: one committed sale, one rejected sale, three instruments remain')
finally:
    sql(f"""
DELETE FROM transactions WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id=HEXTORAW('{owner}'));
DELETE FROM assets WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id=HEXTORAW('{owner}'));
DELETE FROM audit_log WHERE user_id=HEXTORAW('{owner}');
DELETE FROM portfolios WHERE owner_user_id=HEXTORAW('{owner}');
DELETE FROM users WHERE id=HEXTORAW('{owner}');
COMMIT;
""")
