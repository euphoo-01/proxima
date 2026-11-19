#!/usr/bin/env python3
"""Real APP-session security checks; committed fixtures are removed in finally."""
from pathlib import Path
import re
import secrets
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[3]
PREFIX = 'secure-' + uuid.uuid4().hex
LOGINS = [PREFIX + suffix for suffix in ('-a', '-b', '-admin', '-forbidden')]
PASSWORD = 'Secure9A' + secrets.token_hex(16)
ENV = dict(line.split('=', 1) for line in (ROOT / '.env.oracle').read_text().splitlines()
           if '=' in line and not line.startswith('#'))
APP_PASSWORD = ENV['ORACLE_APP_PASSWORD'].strip().strip('"').strip("'")
if any(char in APP_PASSWORD for char in '\r\n"'):
    raise RuntimeError('Unsupported local service-password quoting; no credentials displayed')


def q(value):
    return "'" + value.replace("'", "''") + "'"


def sql(body, app=False):
    prelude = 'SET ECHO OFF\nSET VERIFY OFF\nSET SERVEROUTPUT ON\nSET FEEDBACK OFF\n'
    prelude += 'WHENEVER OSERROR EXIT FAILURE\nWHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK\n'
    if app:
        prelude += f'CONNECT proxima_app/"{APP_PASSWORD}"@localhost:1521/FREEPDB1\n'
    else:
        prelude += 'CONNECT / AS SYSDBA\nALTER SESSION SET CONTAINER=FREEPDB1;\n'
    prelude += 'ALTER SESSION SET CURRENT_SCHEMA=PROXIMA_OWNER;\nSET SERVEROUTPUT ON\n'
    command = ['docker', 'compose', '--env-file', '.env.oracle', '-f', 'docker-compose.oracle.yml',
               'exec', '-T', '-e', 'NLS_LANG=AMERICAN_AMERICA.AL32UTF8', 'oracle', 'sqlplus', '-s', '/nolog']
    try:
        result = subprocess.run(command, input=prelude + body + '\nEXIT;\n', text=True,
                                capture_output=True, cwd=ROOT, timeout=240)
    except subprocess.TimeoutExpired:
        raise RuntimeError('Secure API SQL timed out; captured SQL and credentials suppressed') from None
    if result.returncode or re.search(r'\b(?:ORA|SP2)-\d+', result.stdout + result.stderr):
        codes = ','.join(sorted(set(re.findall(r'\b(?:ORA|SP2)-\d+', result.stdout + result.stderr))))
        raise RuntimeError('Secure API SQL failed: ' + (codes or 'client failure') + '; details suppressed')
    return dict(re.findall(r'^TAG_([A-Z_]+):([^\r\n]+)', result.stdout, re.MULTILINE))


def block(body):
    return 'DECLARE x RAW(16); t VARCHAR2(64); r VARCHAR2(255); c SYS_REFCURSOR; n NUMBER; BEGIN\n' + body + '\nEND;\n/'


def deny(body, code):
    return f"BEGIN {body}; RAISE_APPLICATION_ERROR(-20990,'Expected rejection'); EXCEPTION WHEN OTHERS THEN IF SQLCODE<>{code} THEN RAISE; END IF; END;"


def check(condition, name):
    if not condition:
        raise RuntimeError('Secure API assertion failed: ' + name)


def login(index):
    result = sql(block(f'pkg_auth.login({q(LOGINS[index])},{q(PASSWORD)},t,x,r); '
                       "DBMS_OUTPUT.PUT_LINE('TAG_TOKEN:'||t); DBMS_OUTPUT.PUT_LINE('TAG_ID:'||RAWTOHEX(x)); DBMS_OUTPUT.PUT_LINE('TAG_ROLE:'||r);"), True)
    check(bool(re.fullmatch('[0-9A-F]{64}', result.get('TOKEN', ''))), 'opaque_session_token')
    return result


def scalar(expression, where=''):
    return sql(block(f"SELECT {expression} INTO n FROM {where}; DBMS_OUTPUT.PUT_LINE('TAG_N:'||n);"))['N']


fixture_filter = 'SELECT id FROM users WHERE login IN (' + ','.join(map(q, LOGINS)) + ')'
try:
    schema_before = scalar('COUNT(*)', "dba_tab_columns WHERE owner='PROXIMA_OWNER'")
    other_admins = int(scalar('COUNT(*)', "users WHERE role='Admin' AND account_status='Active'"))
    for index in (0, 1):
        sql(block(f'pkg_auth.register_user({q(LOGINS[index])},{q(PASSWORD)},\'Secure fixture\',x);'), True)
    sql(block(f'pkg_auth.provision_admin({q(LOGINS[2])},{q(PASSWORD)},\'Secure admin fixture\',x);'))
    first, second, admin = login(0), login(1), login(2)
    check(first['ROLE'] == second['ROLE'] == 'PrivateInvestor' and admin['ROLE'] == 'Admin', 'server_assigned_roles')
    a, b, adm = map(lambda item: q(item['TOKEN']), (first, second, admin))
    uid, uid2, adminid = map(lambda item: "HEXTORAW(" + q(item['ID']) + ")", (first, second, admin))
    sql(block(deny(f"pkg_auth.provision_admin({q(PREFIX+'-forbidden')},{q(PASSWORD)},'Forbidden',x)", -20302)
              + deny("DBMS_SESSION.SET_CONTEXT('PROXIMA_CTX','TOKEN_HASH',RPAD('0',64,'0'))", -1031)
              + deny("EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM proxima_owner.users' INTO n", -942)
              + deny("EXECUTE IMMEDIATE 'BEGIN :v := proxima_owner.pkg_password.hash_password(:p); END;' USING OUT r, IN " + q(PASSWORD), -6550)
              + deny("EXECUTE IMMEDIATE 'BEGIN proxima_owner.pkg_ledger.get_balances(:u,:p,:c); END;' USING IN x, IN x, OUT c", -6550)
              + deny(f'pkg_admin.list_users({a},c)', -20302)
              + deny("pkg_portfolios.create_portfolio('bad','Invalid','USD',NULL,NULL,x)", -20301)), True)
    print('PASS service_session_cannot_self_admin_forge_context_or_read_tables')
    created = sql(block(f"pkg_portfolios.create_portfolio({a},'Secure fixture','USD',NULL,NULL,x); DBMS_OUTPUT.PUT_LINE('TAG_PORTFOLIO:'||RAWTOHEX(x)); COMMIT;"), True)
    pid = "HEXTORAW(" + q(created['PORTFOLIO']) + ")"
    instrument = "HEXTORAW('00000000000000000000000000000001')"
    sql(block(f"pkg_portfolios.post_cash({a},{pid},'Deposit',TIMESTAMP '2026-01-01 12:00:00 UTC',1000,'USD',0,x); "
              f"pkg_portfolios.post_trade({a},{pid},{instrument},'Buy',TIMESTAMP '2026-01-02 12:00:00 UTC',2,100,1,x); "
              f"pkg_portfolios.get_balances({a},{pid},c); FETCH c INTO r,n; IF c%NOTFOUND OR r<>'USD' OR n<>799 THEN RAISE_APPLICATION_ERROR(-20991,'Balance'); END IF; CLOSE c; COMMIT;"), True)
    sql("DECLARE c SYS_REFCURSOR; p VARCHAR2(32); nm VARCHAR2(255); ds VARCHAR2(2000); cl VARCHAR2(255); cu VARCHAR2(3); ar NUMBER; t1 TIMESTAMP WITH TIME ZONE; t2 TIMESTAMP WITH TIME ZONE; BEGIN "
        f"pkg_portfolios.list_portfolios({a},c); FETCH c INTO p,nm,ds,cl,cu,ar,t1,t2; IF c%NOTFOUND OR p<>{q(created['PORTFOLIO'])} THEN RAISE_APPLICATION_ERROR(-20991,'Owner list'); END IF; FETCH c INTO p,nm,ds,cl,cu,ar,t1,t2; IF c%FOUND THEN RAISE_APPLICATION_ERROR(-20991,'Extra portfolio'); END IF; CLOSE c; "
        f"pkg_portfolios.list_portfolios({b},c); FETCH c INTO p,nm,ds,cl,cu,ar,t1,t2; IF c%FOUND THEN RAISE_APPLICATION_ERROR(-20991,'Cross-owner list'); END IF; CLOSE c; END;\n/", True)
    sql(block(deny(f'pkg_portfolios.list_assets({b},{pid},c)', -20401)
              + deny(f"pkg_portfolios.post_cash({b},{pid},'Deposit',SYSTIMESTAMP,1,'USD',0,x)", -20401)
              + deny(f"pkg_portfolios.create_portfolio({a},'Invalid label','USD',NULL,'Client',x)", -20402)), True)
    before = scalar('COUNT(*)', f'audit_log WHERE user_id={uid}')
    sql(block(deny(f"pkg_portfolios.post_trade({a},{pid},{instrument},'Sell',TIMESTAMP '2026-01-03 12:00:00 UTC',3,100,0,x)", -20108)
              + f"pkg_portfolios.post_cash({a},{pid},'Deposit',TIMESTAMP '2026-01-04 12:00:00 UTC',10,'USD',0,x); ROLLBACK;"), True)
    check(scalar('COUNT(*)', f'audit_log WHERE user_id={uid}') == before, 'business_rollback_keeps_audit_atomic')
    check(scalar('COUNT(*)', f'transactions WHERE portfolio_id={pid}') == '2', 'failed_and_rolled_back_transactions_absent')
    print('PASS owner_scoped_lists_trades_and_atomic_business_rollback')
    ordered = sql(block(f"pkg_portfolios.create_portfolio({a},'Ordered fixture','USD',NULL,NULL,x); DBMS_OUTPUT.PUT_LINE('TAG_ORDERED:'||RAWTOHEX(x)); COMMIT;"), True)
    op = "HEXTORAW(" + q(ordered['ORDERED']) + ")"
    instant = "TIMESTAMP '2026-02-02 12:00:00 UTC'"
    sql(block(f"pkg_portfolios.post_cash({a},{op},'Deposit',{instant}-INTERVAL '1' DAY,10000,'USD',0,x,0); "
              f"pkg_portfolios.post_trade({a},{op},{instrument},'Buy',{instant},10,100,10,x,1); "
              f"pkg_portfolios.post_trade({a},{op},{instrument},'Buy',{instant},10,120,10,x,2); "
              f"pkg_portfolios.post_trade({a},{op},{instrument},'Sell',{instant},12,150,6,x,3); "
              f"pkg_portfolios.get_balances({a},{op},c); FETCH c INTO r,n; IF c%NOTFOUND OR n<>9574 THEN RAISE_APPLICATION_ERROR(-20991,'Ordered cash'); END IF; CLOSE c; COMMIT;"
              + deny(f"pkg_portfolios.post_trade({a},{op},{instrument},'Buy',{instant},1,100,0,x,2)", -20103)), True)
    check(scalar('quantity*average_buy_price', f'assets WHERE portfolio_id={op}') == '968', 'ordered_api_remaining_basis')
    print('PASS authenticated_same_timestamp_ordered_trades_and_duplicate_rejection')
    sql(block(deny(f'pkg_portfolios.archive_portfolio({b},{pid})', -20401)
              + deny(f'pkg_admin.delete_archived_portfolio({adm},{pid})', -20504)
              + f'pkg_portfolios.archive_portfolio({a},{pid}); COMMIT;'
              + deny(f"pkg_portfolios.post_cash({a},{pid},'Deposit',SYSTIMESTAMP,1,'USD',0,x)", -20401)
              + deny(f'pkg_admin.restore_portfolio({b},{pid})', -20302)), True)
    sql(block(f"pkg_admin.restore_portfolio({adm},{pid}); COMMIT; pkg_portfolios.get_balances({a},{pid},c); FETCH c INTO r,n; IF c%NOTFOUND OR n<>799 THEN RAISE_APPLICATION_ERROR(-20991,'Restored balance'); END IF; CLOSE c;"), True)
    child = sql(block(f"INSERT INTO import_batches(portfolio_id,imported_by_user_id,file_name,status) VALUES({pid},{uid},'fixture.csv','Preview') RETURNING id INTO x; DBMS_OUTPUT.PUT_LINE('TAG_BATCH:'||RAWTOHEX(x)); "
                      "INSERT INTO import_rows(batch_id,row_number,raw_data,validation_status) VALUES(x,1,'fixture','Valid'); "
                      f"INSERT INTO goals(portfolio_id,title,target_amount,currency,monthly_contribution,expected_annual_return_percent) VALUES({pid},'Fixture',100,'USD',1,1); "
                      f"INSERT INTO portfolio_snapshots(portfolio_id,snapshot_date,total_value,invested_amount,cash_balance,currency) VALUES({pid},DATE '2026-01-02',1000,1000,799,'USD'); "
                      f"INSERT INTO tags(owner_user_id,name) VALUES({uid},'Fixture tag') RETURNING id INTO x; DBMS_OUTPUT.PUT_LINE('TAG_TAG:'||RAWTOHEX(x)); "
                      f"INSERT INTO asset_tags(asset_id,tag_id) SELECT id,x FROM assets WHERE portfolio_id={pid}; COMMIT;"))
    batch, tag = ("HEXTORAW(" + q(child[key]) + ")" for key in ('BATCH', 'TAG'))
    sql(block(f'pkg_portfolios.archive_portfolio({a},{pid}); COMMIT;'), True)
    audited = f"audit_log WHERE COALESCE(JSON_VALUE(metadata_json,'$.portfolio_id'),JSON_VALUE(metadata_json,'$.target_id'))={q(created['PORTFOLIO'])}"
    retained = int(scalar('COUNT(*)', audited))
    sql(block(deny(f'pkg_admin.delete_archived_portfolio({b},{pid})', -20302)
              + f'pkg_admin.delete_archived_portfolio({adm},{pid}); COMMIT;'), True)
    for table in ('portfolios', 'transactions', 'assets', 'import_batches', 'goals', 'portfolio_snapshots'):
        column = 'id' if table == 'portfolios' else 'portfolio_id'
        check(scalar('COUNT(*)', f'{table} WHERE {column}={pid}') == '0', 'hard_delete_removes_' + table)
    check(scalar('COUNT(*)', f'import_rows WHERE batch_id={batch}') == '0', 'hard_delete_removes_import_rows')
    check(scalar('COUNT(*)', f'asset_tags WHERE tag_id={tag}') == '0', 'hard_delete_removes_asset_tag_links')
    check(scalar('COUNT(*)', f'tags WHERE id={tag}') == '1', 'hard_delete_retains_owner_tag')
    check(int(scalar('COUNT(*)', audited)) == retained + 1, 'hard_delete_retains_prior_audit_and_records_deletion')
    print('PASS owner_archive_admin_restore_and_hard_delete_children_preserving_audit')
    sql(block(f"pkg_admin.set_role({adm},{uid},'FinancialConsultant'); COMMIT;"), True)
    sql(block(deny(f'x:=pkg_auth.require_user({a})', -20301)), True)
    first = login(0); a = q(first['TOKEN'])
    check(first['ROLE'] == 'FinancialConsultant', 'role_change_visible_after_reauthentication')
    sql(block(f"pkg_portfolios.create_portfolio({a},'Client fixture','USD',NULL,'Client',x); COMMIT;"
              + deny(f"pkg_admin.set_role({a},{uid},'Admin')", -20302)), True)
    sql(block(f"pkg_admin.set_status({adm},{uid2},'Blocked'); COMMIT;"), True)
    sql(block(deny(f'x:=pkg_auth.require_user({b})', -20301)), True)
    sql(block(f"pkg_admin.set_status({adm},{uid2},'Active'); COMMIT;"), True)
    second = login(1); b = q(second['TOKEN'])
    sql(block(f'pkg_auth.logout({b}); ' + deny(f'x:=pkg_auth.require_user({b})', -20301)), True)
    sql(block(f"UPDATE auth_sessions SET created_at=SYSTIMESTAMP-INTERVAL '2' DAY,expires_at=SYSTIMESTAMP-INTERVAL '1' DAY WHERE user_id={uid}; COMMIT;"))
    sql(block(deny(f'x:=pkg_auth.require_user({a})', -20301)), True)
    print('PASS role_revocation_block_logout_and_expiry_reject_existing_tokens')
    for _ in range(5):
        sql(block(deny(f"pkg_auth.login({q(LOGINS[1])},{q('Wrong9A'+secrets.token_hex(12))},t,x,r)", -20301) + ' ROLLBACK;'), True)
    check(scalar('failed_unlock_attempts', f'users WHERE id={uid2}') == '5', 'failed_attempt_counter_survives_caller_rollback')
    check(scalar("CASE WHEN account_status='Blocked' THEN 1 ELSE 0 END", f'users WHERE id={uid2}') == '1', 'five_failures_block_account')
    sql(block(deny(f'pkg_auth.login({q(LOGINS[1])},{q(PASSWORD)},t,x,r)', -20301)), True)
    print('PASS persistent_failed_login_counter_and_generic_blocked_login_error')
    if other_admins == 0:
        sql(block(deny(f"pkg_admin.set_role({adm},{adminid},'PrivateInvestor')", -20501)
                  + deny(f"pkg_admin.set_status({adm},{adminid},'Blocked')", -20501)), True)
        print('PASS last_active_admin_cannot_be_demoted_or_blocked')
    else:
        print('SKIP last_active_admin_guard: preexisting active administrator retained')
    check(scalar('COUNT(*)', "dba_tab_columns WHERE owner='PROXIMA_OWNER'") == schema_before, 'no_physical_schema_changes')
finally:
    # Exact random logins only; dependency order avoids touching preexisting users.
    sql(f"""DELETE FROM asset_tags WHERE asset_id IN (SELECT id FROM assets WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id IN ({fixture_filter})));
DELETE FROM import_rows WHERE batch_id IN (SELECT id FROM import_batches WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id IN ({fixture_filter})));
DELETE FROM import_batches WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id IN ({fixture_filter}));
DELETE FROM goals WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id IN ({fixture_filter}));
DELETE FROM portfolio_snapshots WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id IN ({fixture_filter}));
DELETE FROM tags WHERE owner_user_id IN ({fixture_filter});
DELETE FROM transactions WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id IN ({fixture_filter}));
DELETE FROM assets WHERE portfolio_id IN (SELECT id FROM portfolios WHERE owner_user_id IN ({fixture_filter}));
DELETE FROM portfolios WHERE owner_user_id IN ({fixture_filter});
DELETE FROM auth_sessions WHERE user_id IN ({fixture_filter});
DELETE FROM audit_log WHERE user_id IN ({fixture_filter});
DELETE FROM users WHERE id IN ({fixture_filter});
COMMIT;""")
    check(scalar('COUNT(*)', 'users WHERE login IN (' + ','.join(map(q, LOGINS)) + ')') == '0', 'fixture_accounts_removed')
    print('PASS fixture_accounts_sessions_business_rows_and_audit_cleaned')
print('Secure API integration assertions passed.')
