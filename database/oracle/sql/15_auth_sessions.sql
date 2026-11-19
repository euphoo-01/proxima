WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
  n PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO n FROM all_tables WHERE owner=SYS_CONTEXT('USERENV','CURRENT_SCHEMA')
    AND table_name='AUTH_SESSIONS';
  IF n=0 THEN
    EXECUTE IMMEDIATE q'~
CREATE TABLE auth_sessions (
  id RAW(16) DEFAULT SYS_GUID() NOT NULL,
  user_id RAW(16) NOT NULL,
  token_hash RAW(32) NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
  expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
  revoked_at TIMESTAMP WITH TIME ZONE,
  CONSTRAINT pk_auth_sessions PRIMARY KEY (id),
  CONSTRAINT uq_auth_sessions_token UNIQUE (token_hash),
  CONSTRAINT fk_er_33 FOREIGN KEY (user_id) REFERENCES users (id),
  CONSTRAINT ck_auth_sessions_expiry CHECK (expires_at>created_at)
)
~';
    EXECUTE IMMEDIATE 'CREATE INDEX ix_auth_sessions_user ON auth_sessions(user_id)';
  END IF;
END;
/
