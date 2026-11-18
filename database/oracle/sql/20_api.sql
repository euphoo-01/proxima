WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_reference AUTHID DEFINER AS
  PROCEDURE get_server_info(p_result OUT SYS_REFCURSOR);
  PROCEDURE list_currencies(p_result OUT SYS_REFCURSOR);
  PROCEDURE list_instruments(p_result OUT SYS_REFCURSOR);
END pkg_reference;
/
CREATE OR REPLACE PACKAGE BODY pkg_reference AS
  PROCEDURE get_server_info(p_result OUT SYS_REFCURSOR) IS
  BEGIN
    OPEN p_result FOR SELECT SYS_CONTEXT('USERENV','DB_NAME') AS database_name,
      SYS_CONTEXT('USERENV','SESSION_USER') AS session_user,
      SYSTIMESTAMP AS server_time FROM dual;
  END;
  PROCEDURE list_currencies(p_result OUT SYS_REFCURSOR) IS
  BEGIN
    OPEN p_result FOR SELECT code, name, symbol FROM currencies ORDER BY code;
  END;
  PROCEDURE list_instruments(p_result OUT SYS_REFCURSOR) IS
  BEGIN
    OPEN p_result FOR SELECT RAWTOHEX(id) AS instrument_id, ticker, name,
      currency AS currency_code, type AS instrument_type FROM instruments ORDER BY ticker;
  END;
END pkg_reference;
/
