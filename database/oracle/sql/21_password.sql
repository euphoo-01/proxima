WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_password AUTHID DEFINER AS
  FUNCTION derive_key(p_password VARCHAR2,p_salt RAW,p_iterations PLS_INTEGER) RETURN RAW;
  FUNCTION hash_password(p_password VARCHAR2) RETURN VARCHAR2;
  FUNCTION verify_password(p_password VARCHAR2,p_encoded VARCHAR2) RETURN BOOLEAN;
END pkg_password;
/
CREATE OR REPLACE PACKAGE BODY pkg_password AS
  c_iterations CONSTANT PLS_INTEGER:=600000;
  c_prefix CONSTANT VARCHAR2(21):='PBKDF2-SHA256$600000$';
  FUNCTION derive_key(p_password VARCHAR2,p_salt RAW,p_iterations PLS_INTEGER) RETURN RAW IS
    password_bytes RAW(512); u RAW(32); result RAW(32);
  BEGIN
    IF p_password IS NULL OR LENGTH(p_password)>128 OR p_salt IS NULL
      OR UTL_RAW.LENGTH(p_salt)>32 OR p_iterations IS NULL OR p_iterations<1 OR p_iterations>c_iterations THEN
      RAISE_APPLICATION_ERROR(-20200,'Invalid PBKDF2 parameters');
    END IF;
    password_bytes:=UTL_I18N.STRING_TO_RAW(p_password,'AL32UTF8');
    u:=DBMS_CRYPTO.MAC(UTL_RAW.CONCAT(p_salt,HEXTORAW('00000001')),DBMS_CRYPTO.HMAC_SH256,password_bytes);
    result:=u;
    FOR i IN 2..p_iterations LOOP
      u:=DBMS_CRYPTO.MAC(u,DBMS_CRYPTO.HMAC_SH256,password_bytes);
      result:=UTL_RAW.BIT_XOR(result,u);
    END LOOP;
    RETURN result;
  END;
  FUNCTION hash_password(p_password VARCHAR2) RETURN VARCHAR2 IS
    salt RAW(16);
  BEGIN
    IF p_password IS NULL OR LENGTH(p_password)<12 OR LENGTH(p_password)>128 THEN
      RAISE_APPLICATION_ERROR(-20201,'Password must contain 12 to 128 characters');
    END IF;
    salt:=DBMS_CRYPTO.RANDOMBYTES(16);
    RETURN c_prefix||RAWTOHEX(salt)||'$'||RAWTOHEX(derive_key(p_password,salt,c_iterations));
  END;
  FUNCTION verify_password(p_password VARCHAR2,p_encoded VARCHAR2) RETURN BOOLEAN IS
    expected RAW(32); actual RAW(32); difference RAW(32); mismatch PLS_INTEGER:=0;
  BEGIN
    IF p_password IS NULL OR LENGTH(p_password)<12 OR LENGTH(p_password)>128 OR p_encoded IS NULL THEN RETURN FALSE; END IF;
    IF LENGTH(p_encoded)<>118 OR NOT REGEXP_LIKE(p_encoded,
      '^PBKDF2-SHA256\$600000\$[0-9A-F]{32}\$[0-9A-F]{64}$','c') THEN RETURN FALSE; END IF;
    expected:=HEXTORAW(SUBSTR(p_encoded,55,64));
    actual:=derive_key(p_password,HEXTORAW(SUBSTR(p_encoded,22,32)),c_iterations);
    difference:=UTL_RAW.BIT_XOR(expected,actual);
    FOR i IN 1..32 LOOP
      mismatch:=mismatch+TO_NUMBER(RAWTOHEX(UTL_RAW.SUBSTR(difference,i,1)),'XX');
    END LOOP;
    RETURN mismatch=0;
  END;
END pkg_password;
/
