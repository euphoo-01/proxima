SET SERVEROUTPUT ON
VARIABLE result REFCURSOR
EXEC proxima_owner.pkg_reference.get_server_info(:result);
PRINT result
EXEC proxima_owner.pkg_reference.list_currencies(:result);
PRINT result
EXEC proxima_owner.pkg_reference.list_instruments(:result);
PRINT result
DECLARE
  c SYS_REFCURSOR;
  code VARCHAR2(3); name VARCHAR2(255); symbol VARCHAR2(255);
  n PLS_INTEGER := 0;
  privileges PLS_INTEGER;
  instrument_id VARCHAR2(32); ticker VARCHAR2(255); instrument_name VARCHAR2(255);
  currency_code VARCHAR2(3); instrument_type VARCHAR2(255);
BEGIN
  proxima_owner.pkg_reference.list_currencies(c);
  LOOP
    FETCH c INTO code, name, symbol;
    EXIT WHEN c%NOTFOUND;
    n := n + 1;
  END LOOP;
  CLOSE c;
  IF n <> 3 THEN RAISE_APPLICATION_ERROR(-20010,'Expected 3 seed currencies'); END IF;
  n := 0;
  proxima_owner.pkg_reference.list_instruments(c);
  LOOP
    FETCH c INTO instrument_id, ticker, instrument_name, currency_code, instrument_type;
    EXIT WHEN c%NOTFOUND;
    IF LENGTH(instrument_id) <> 32 THEN RAISE_APPLICATION_ERROR(-20012,'Invalid instrument identifier'); END IF;
    n := n + 1;
  END LOOP;
  CLOSE c;
  IF n <> 4 THEN RAISE_APPLICATION_ERROR(-20013,'Expected 4 seed instruments'); END IF;
  SELECT COUNT(*) INTO privileges FROM user_tab_privs_recd
    WHERE owner = 'PROXIMA_OWNER' AND privilege <> 'EXECUTE';
  IF privileges <> 0 THEN RAISE_APPLICATION_ERROR(-20011,'App has direct data access'); END IF;
  DBMS_OUTPUT.PUT_LINE('Public catalog smoke passed');
END;
/
