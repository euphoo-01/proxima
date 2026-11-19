WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET DEFINE OFF
DECLARE
  h1 VARCHAR2(512); h2 VARCHAR2(512); start_tick NUMBER; seconds_value NUMBER;
  PROCEDURE check_true(p_value BOOLEAN,p_name VARCHAR2) IS
  BEGIN
    IF p_value IS NULL OR NOT p_value THEN RAISE_APPLICATION_ERROR(-20290,p_name); END IF;
    DBMS_OUTPUT.PUT_LINE('PASS '||p_name);
  END;
  PROCEDURE vector(p_password VARCHAR2,p_salt VARCHAR2,p_iterations PLS_INTEGER,p_hex VARCHAR2,p_name VARCHAR2) IS
  BEGIN
    check_true(RAWTOHEX(pkg_password.derive_key(p_password,UTL_I18N.STRING_TO_RAW(p_salt,'AL32UTF8'),p_iterations))=p_hex,p_name);
  END;
BEGIN
  vector('password','salt',1,'120FB6CFFCF8B32C43E7225256C4F837A86548C92CCC35480805987CB70BE17B','vector_sha256_iteration_1');
  vector('password','salt',2,'AE4D0C95AF6B46D32D0ADFF928F06DD02A303F8EF3C251DFD6E2D85A95474C43','vector_sha256_iteration_2');
  vector('password','salt',4096,'C5E478D59288C841AA530DB6845C4C8D962893A001CE4E11A4963873AA98134A','vector_sha256_iteration_4096');
  vector(UNISTR('\043F\0430\0440\043E\043B\044C\D83D\DD10'),UNISTR('\0441\043E\043B\044C'),2,
    'A9CA8BF70070E33EB030B70A63A29C4EFDA7A96A70D670A961BFC1DB94997F8A','vector_utf8_iteration_2');
  start_tick:=DBMS_UTILITY.GET_TIME;
  h1:=pkg_password.hash_password('correct horse battery staple');
  seconds_value:=(DBMS_UTILITY.GET_TIME-start_tick)/100;
  DBMS_OUTPUT.PUT_LINE('production_hash_600000_seconds='||TO_CHAR(seconds_value,'FM999990D00','NLS_NUMERIC_CHARACTERS=''.,'''));
  h2:=pkg_password.hash_password('correct horse battery staple');
  check_true(LENGTH(h1)=118 AND SUBSTR(h1,1,21)='PBKDF2-SHA256$600000$','production_format');
  check_true(SUBSTR(h1,22,32)<>SUBSTR(h2,22,32),'independent_random_salts');
  check_true(pkg_password.verify_password('correct horse battery staple',h1),'production_correct_password');
  check_true(NOT pkg_password.verify_password('incorrect horse battery staple',h1),'production_wrong_password');
  check_true(NOT pkg_password.verify_password('correct horse battery staple',REPLACE(h1,'600000','1')),'reject_weakened_work_factor');
  check_true(NOT pkg_password.verify_password('correct horse battery staple',h1||CHR(10)),'reject_trailing_content');
  check_true(NOT pkg_password.verify_password('correct horse battery staple',SUBSTR(h1,1,117)||'Z'),'reject_invalid_hex');
  check_true(NOT pkg_password.verify_password(NULL,h1),'reject_null_password');
  check_true(NOT pkg_password.verify_password('short',h1),'reject_short_password');
  check_true(NOT pkg_password.verify_password('correct horse battery staple',NULL),'reject_null_format');
  BEGIN
    h2:=pkg_password.hash_password('short');
    RAISE_APPLICATION_ERROR(-20291,'short_password_was_accepted');
  EXCEPTION WHEN OTHERS THEN IF SQLCODE<>-20201 THEN RAISE; END IF;
  END;
  DBMS_OUTPUT.PUT_LINE('PASS hash_rejects_short_password');
  BEGIN
    h2:=pkg_password.hash_password(RPAD('a',129,'a'));
    RAISE_APPLICATION_ERROR(-20292,'long_password_was_accepted');
  EXCEPTION WHEN OTHERS THEN IF SQLCODE<>-20201 THEN RAISE; END IF;
  END;
  DBMS_OUTPUT.PUT_LINE('PASS hash_rejects_long_password');
  BEGIN
    h2:=RAWTOHEX(pkg_password.derive_key('password',HEXTORAW('73616C74'),0));
    RAISE_APPLICATION_ERROR(-20293,'zero_iterations_were_accepted');
  EXCEPTION WHEN OTHERS THEN IF SQLCODE<>-20200 THEN RAISE; END IF;
  END;
  DBMS_OUTPUT.PUT_LINE('PASS derive_rejects_zero_iterations');
END;
/
