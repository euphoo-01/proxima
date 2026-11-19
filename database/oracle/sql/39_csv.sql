WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
CREATE OR REPLACE PACKAGE pkg_csv AUTHID DEFINER AS
  PROCEDURE preview(p_token VARCHAR2,p_portfolio_id RAW,p_file_name VARCHAR2,p_content CLOB,p_batch_id OUT RAW);
  PROCEDURE list_rows(p_token VARCHAR2,p_batch_id RAW,p_result OUT SYS_REFCURSOR);
  PROCEDURE correct_row(p_token VARCHAR2,p_batch_id RAW,p_row_number NUMBER,p_raw_data CLOB);
  PROCEDURE validate_batch(p_token VARCHAR2,p_batch_id RAW);
  PROCEDURE commit_batch(p_token VARCHAR2,p_batch_id RAW);
  PROCEDURE export_transactions(p_token VARCHAR2,p_portfolio_id RAW,p_content OUT CLOB);
END pkg_csv;
/
CREATE OR REPLACE PACKAGE BODY pkg_csv AS
  TYPE fields_type IS TABLE OF VARCHAR2(32767) INDEX BY PLS_INTEGER;
  c_header CONSTANT VARCHAR2(200):='trade_date,ticker,name,type,quantity,price,currency,fee,amount,operation_order';
  c_old_header CONSTANT VARCHAR2(200):='trade_date,ticker,name,type,quantity,price,currency,fee';
  PROCEDURE invalid(p_message VARCHAR2) IS
  BEGIN RAISE_APPLICATION_ERROR(-20600,p_message); END;
  FUNCTION actor(p_token VARCHAR2) RETURN RAW IS
    result RAW(16); role_value VARCHAR2(255);
  BEGIN
    pkg_auth.begin_request(p_token); result:=pkg_auth.current_user_id;
    SELECT role INTO role_value FROM users WHERE id=result;
    IF role_value NOT IN ('PrivateInvestor','FinancialConsultant') THEN invalid('Investor or consultant required'); END IF;
    RETURN result;
  END;
  PROCEDURE lock_portfolio(p_user RAW,p_portfolio RAW) IS
    owner_value RAW(16); archived NUMBER;
  BEGIN
    SELECT owner_user_id,is_archived INTO owner_value,archived FROM portfolios WHERE id=p_portfolio FOR UPDATE;
    IF owner_value<>p_user OR archived<>0 THEN invalid('Portfolio unavailable'); END IF;
  EXCEPTION WHEN NO_DATA_FOUND THEN invalid('Portfolio unavailable');
  END;
  PROCEDURE lock_batch(p_user RAW,p_batch RAW,p_portfolio OUT RAW) IS
    status_value VARCHAR2(255);
  BEGIN
    SELECT portfolio_id INTO p_portfolio FROM import_batches WHERE id=p_batch;
    lock_portfolio(p_user,p_portfolio);
    SELECT status INTO status_value FROM import_batches WHERE id=p_batch FOR UPDATE;
    IF status_value='Imported' THEN invalid('Batch already imported'); END IF;
  EXCEPTION WHEN NO_DATA_FOUND THEN invalid('Batch unavailable');
  END;
  PROCEDURE parse_record(p_record VARCHAR2,p_fields OUT fields_type) IS
    value_text VARCHAR2(32767):=''; ch VARCHAR2(1); state PLS_INTEGER:=0; n PLS_INTEGER:=1; i PLS_INTEGER:=1;
  BEGIN
    p_fields.DELETE;
    WHILE i<=LENGTH(p_record) LOOP
      ch:=SUBSTR(p_record,i,1);
      IF state=1 THEN
        IF ch='"' THEN
          IF SUBSTR(p_record,i+1,1)='"' THEN value_text:=value_text||'"'; i:=i+1;
          ELSE state:=2; END IF;
        ELSE value_text:=value_text||ch; END IF;
      ELSIF ch=',' THEN p_fields(n):=value_text; n:=n+1; value_text:=''; state:=0;
      ELSIF state=2 THEN invalid('Unexpected content after closing quote');
      ELSIF ch='"' THEN
        IF value_text IS NOT NULL THEN invalid('Quote inside unquoted field'); END IF;
        state:=1;
      ELSIF ch IN (CHR(10),CHR(13)) THEN invalid('Unquoted newline in record');
      ELSE value_text:=value_text||ch; END IF;
      i:=i+1;
    END LOOP;
    IF state=1 THEN invalid('Unclosed quoted field'); END IF;
    p_fields(n):=value_text;
  END;
  FUNCTION number_value(p_text VARCHAR2) RETURN NUMBER IS
    result NUMBER;
  BEGIN
    IF p_text IS NULL OR LENGTH(p_text)>55 OR NOT REGEXP_LIKE(p_text,'^[+-]?[0-9]+([.][0-9]+)?$','c') THEN invalid('Invalid decimal number'); END IF;
    BEGIN
      result:=TO_NUMBER(p_text,'9999999999999999999999999999D99999999999999999999','NLS_NUMERIC_CHARACTERS=''.,''');
    EXCEPTION WHEN VALUE_ERROR OR INVALID_NUMBER THEN invalid('Invalid decimal number'); END;
    RETURN result;
  END;
  FUNCTION date_value(p_text VARCHAR2) RETURN TIMESTAMP WITH TIME ZONE IS
    result TIMESTAMP WITH TIME ZONE; text_value VARCHAR2(100):=p_text;
  BEGIN
    IF text_value IS NULL OR NOT REGEXP_LIKE(text_value,'^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}([.][0-9]{1,9})?(Z|[+-][0-9]{2}:[0-9]{2})$','c') THEN invalid('Expected ISO8601 timestamp with timezone'); END IF;
    text_value:=REPLACE(text_value,'Z','+00:00');
    BEGIN
      IF INSTR(text_value,'.')>0 THEN result:=TO_TIMESTAMP_TZ(text_value,'YYYY-MM-DD"T"HH24:MI:SS.FF9TZH:TZM');
      ELSE result:=TO_TIMESTAMP_TZ(text_value,'YYYY-MM-DD"T"HH24:MI:SSTZH:TZM'); END IF;
    EXCEPTION WHEN OTHERS THEN
      IF SQLCODE BETWEEN -1899 AND -1800 THEN invalid('Invalid calendar timestamp'); ELSE RAISE; END IF;
    END;
    RETURN result;
  END;
  PROCEDURE normalize(p_raw VARCHAR2,p_json OUT CLOB) IS
    f fields_type; moment TIMESTAMP WITH TIME ZONE; instrument_value RAW(16);
    qty NUMBER; price_value NUMBER; fee NUMBER; amount_value NUMBER; order_value NUMBER:=0; currency_count NUMBER;
  BEGIN
    parse_record(p_raw,f);
    IF f.COUNT NOT IN (8,10) THEN invalid('Expected 8 or 10 CSV columns'); END IF;
    moment:=date_value(f(1)); qty:=ROUND(number_value(f(5)),10); price_value:=ROUND(number_value(f(6)),10);
    fee:=ROUND(number_value(f(8)),10);
    IF f(4) IS NULL OR f(4) NOT IN ('Deposit','Withdrawal','Buy','Sell') THEN invalid('Unsupported operation type'); END IF;
    IF f(7) IS NULL OR NOT REGEXP_LIKE(f(7),'^[A-Z]{3}$','c') THEN invalid('Invalid currency'); END IF;
    SELECT COUNT(*) INTO currency_count FROM currencies WHERE code=f(7);
    IF currency_count<>1 THEN invalid('Unknown currency'); END IF;
    IF f.COUNT=10 THEN order_value:=number_value(f(10)); END IF;
    IF order_value<0 OR order_value<>TRUNC(order_value) OR order_value>999999999999999999 OR fee<0 THEN invalid('Invalid fee or operation order'); END IF;
    IF f(4) IN ('Buy','Sell') THEN
      IF qty<=0 OR price_value<=0 THEN invalid('Trade quantity and price must be positive'); END IF;
      BEGIN SELECT id INTO instrument_value FROM instruments WHERE ticker=f(2) AND currency=f(7);
      EXCEPTION WHEN NO_DATA_FOUND THEN invalid('Unknown ticker/currency'); WHEN TOO_MANY_ROWS THEN invalid('Ambiguous ticker/currency'); END;
      amount_value:=ROUND(qty*price_value,10);
      IF f.COUNT=10 AND f(9) IS NOT NULL AND number_value(f(9))<>amount_value THEN invalid('Trade amount disagrees with quantity and price'); END IF;
    ELSE
      IF qty<>0 OR price_value<>0 OR f(2) IS NOT NULL OR f.COUNT<>10 THEN invalid('Cash requires empty ticker, zero quantity/price and amount column'); END IF;
      amount_value:=ROUND(number_value(f(9)),10);
    END IF;
    IF amount_value<=0 OR ABS(qty)>=1e18 OR ABS(price_value)>=1e18 OR ABS(fee)>=1e18 OR amount_value>=1e18 THEN
      invalid('Amount must be positive and numeric values fit NUMBER(28,10)'); END IF;
    SELECT JSON_OBJECT('trade_date' VALUE TO_CHAR(moment,'YYYY-MM-DD"T"HH24:MI:SS.FF9TZH:TZM'),
      'instrument_id' VALUE RAWTOHEX(instrument_value),'type' VALUE f(4),'quantity' VALUE qty,'price' VALUE price_value,
      'currency' VALUE f(7),'fee' VALUE fee,'amount' VALUE amount_value,'operation_order' VALUE order_value RETURNING CLOB)
      INTO p_json FROM dual;
  END;
  PROCEDURE stage(p_batch RAW,p_number NUMBER,p_raw VARCHAR2) IS
    json_value CLOB; error_value VARCHAR2(2000);
  BEGIN
    BEGIN normalize(p_raw,json_value);
    EXCEPTION WHEN OTHERS THEN
      IF SQLCODE=-20600 THEN error_value:=SUBSTR(SQLERRM,1,2000); ELSE RAISE; END IF;
    END;
    INSERT INTO import_rows(batch_id,row_number,raw_data,parsed_data,validation_status,error_message)
      VALUES(p_batch,p_number,p_raw,json_value,CASE WHEN error_value IS NULL THEN 'Valid' ELSE 'Invalid' END,error_value);
  END;
  PROCEDURE preview(p_token VARCHAR2,p_portfolio_id RAW,p_file_name VARCHAR2,p_content CLOB,p_batch_id OUT RAW) IS
    user_value RAW(16); hash_value RAW(32); n NUMBER; position_value PLS_INTEGER:=1; length_value PLS_INTEGER;
    record_value VARCHAR2(32767):=''; row_value NUMBER:=-1; quoted BOOLEAN:=FALSE; ch VARCHAR2(1);
    chunk_value VARCHAR2(8000); chunk_start PLS_INTEGER:=0; header_value VARCHAR2(200);
    FUNCTION next_char(p_position PLS_INTEGER) RETURN VARCHAR2 IS
    BEGIN
      IF p_position<chunk_start OR p_position>=chunk_start+NVL(LENGTH(chunk_value),0) THEN
        chunk_start:=p_position; chunk_value:=DBMS_LOB.SUBSTR(p_content,8000,p_position);
      END IF;
      RETURN SUBSTR(chunk_value,p_position-chunk_start+1,1);
    END;
    PROCEDURE finish_record IS
      f fields_type; canonical VARCHAR2(200):='';
    BEGIN
      IF row_value=-1 THEN
        parse_record(record_value,f);
        FOR j IN 1..f.COUNT LOOP IF j>1 THEN canonical:=canonical||','; END IF; canonical:=canonical||f(j); END LOOP;
        IF canonical NOT IN (c_header,c_old_header) THEN invalid('Unsupported CSV header'); END IF;
        header_value:=canonical;
      ELSE
        IF row_value>=1000 THEN invalid('CSV row limit exceeded'); END IF;
        stage(p_batch_id,row_value+1,record_value);
      END IF;
      row_value:=row_value+1; record_value:='';
    END;
  BEGIN
    SAVEPOINT csv_api; user_value:=actor(p_token); lock_portfolio(user_value,p_portfolio_id);
    length_value:=DBMS_LOB.GETLENGTH(p_content);
    IF length_value IS NULL OR length_value=0 OR length_value>262144 OR TRIM(p_file_name) IS NULL THEN invalid('CSV must contain 1..262144 characters and a filename'); END IF;
    hash_value:=DBMS_CRYPTO.HASH(p_content,DBMS_CRYPTO.HASH_SH256);
    SELECT COUNT(*) INTO n FROM import_batches WHERE portfolio_id=p_portfolio_id AND file_hash=hash_value;
    IF n>0 THEN RAISE_APPLICATION_ERROR(-20601,'File already staged for this portfolio'); END IF;
    p_batch_id:=SYS_GUID();
    INSERT INTO import_batches(id,portfolio_id,imported_by_user_id,file_name,status,file_hash)
      VALUES(p_batch_id,p_portfolio_id,user_value,p_file_name,'Preview',hash_value);
    IF next_char(1)=UNISTR('\FEFF') THEN position_value:=2; END IF;
    WHILE position_value<=length_value LOOP
      ch:=next_char(position_value);
      IF ch='"' THEN quoted:=NOT quoted; END IF;
      IF NOT quoted AND ch IN (CHR(10),CHR(13)) THEN
        finish_record;
        IF ch=CHR(13) AND next_char(position_value+1)=CHR(10) THEN position_value:=position_value+1; END IF;
      ELSE
        IF NVL(LENGTH(record_value),0)>=8000 THEN invalid('CSV record exceeds 8000 characters'); END IF;
        record_value:=record_value||ch;
      END IF;
      position_value:=position_value+1;
    END LOOP;
    IF record_value IS NOT NULL THEN finish_record; END IF;
    IF row_value<1 THEN invalid('CSV contains no data rows'); END IF;
    UPDATE import_batches SET rejected_count=(SELECT COUNT(*) FROM import_rows WHERE batch_id=p_batch_id AND validation_status='Invalid') WHERE id=p_batch_id;
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO csv_api; p_batch_id:=NULL; RAISE;
  END;
  PROCEDURE list_rows(p_token VARCHAR2,p_batch_id RAW,p_result OUT SYS_REFCURSOR) IS
    user_value RAW(16); portfolio_value RAW(16); owner_value RAW(16);
  BEGIN
    user_value:=actor(p_token);
    SELECT p.owner_user_id INTO owner_value FROM import_batches b JOIN portfolios p ON p.id=b.portfolio_id WHERE b.id=p_batch_id;
    IF owner_value<>user_value THEN invalid('Batch unavailable'); END IF;
    OPEN p_result FOR SELECT row_number,raw_data,parsed_data,validation_status,error_message FROM import_rows WHERE batch_id=p_batch_id ORDER BY row_number;
  END;
  PROCEDURE correct_row(p_token VARCHAR2,p_batch_id RAW,p_row_number NUMBER,p_raw_data CLOB) IS
    user_value RAW(16); portfolio_value RAW(16); n NUMBER;
  BEGIN
    SAVEPOINT csv_api; user_value:=actor(p_token); lock_batch(user_value,p_batch_id,portfolio_value);
    IF p_raw_data IS NULL OR DBMS_LOB.GETLENGTH(p_raw_data)>8000 THEN invalid('Invalid replacement row length'); END IF;
    SELECT COUNT(*) INTO n FROM import_rows WHERE batch_id=p_batch_id AND row_number=p_row_number;
    IF n<>1 THEN invalid('Row unavailable'); END IF;
    DELETE FROM import_rows WHERE batch_id=p_batch_id AND row_number=p_row_number;
    stage(p_batch_id,p_row_number,DBMS_LOB.SUBSTR(p_raw_data,8000,1));
    UPDATE import_batches SET status='Preview',imported_count=0,
      rejected_count=(SELECT COUNT(*) FROM import_rows WHERE batch_id=p_batch_id AND validation_status='Invalid') WHERE id=p_batch_id;
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO csv_api; RAISE;
  END;
  PROCEDURE apply_rows(p_user RAW,p_batch RAW,p_portfolio RAW) IS
    asset_value RAW(16); instrument_value RAW(16); moment_value TIMESTAMP WITH TIME ZONE;
  BEGIN
    FOR r IN (SELECT j.* FROM import_rows ir CROSS APPLY JSON_TABLE(ir.parsed_data,'$' COLUMNS(
      moment VARCHAR2(100) PATH '$.trade_date',instrument VARCHAR2(32) PATH '$.instrument_id',
      kind VARCHAR2(20) PATH '$.type',qty NUMBER PATH '$.quantity',price NUMBER PATH '$.price',
      currency VARCHAR2(3) PATH '$.currency',fee NUMBER PATH '$.fee',amount NUMBER PATH '$.amount',
      operation_order NUMBER PATH '$.operation_order')) j WHERE ir.batch_id=p_batch ORDER BY ir.row_number) LOOP
      asset_value:=NULL;
      IF r.instrument IS NOT NULL THEN
        instrument_value:=HEXTORAW(r.instrument);
        MERGE INTO assets a USING (SELECT p_portfolio portfolio_id,instrument_value instrument_id FROM dual) s
          ON(a.portfolio_id=s.portfolio_id AND a.instrument_id=s.instrument_id)
          WHEN NOT MATCHED THEN INSERT(portfolio_id,instrument_id,quantity) VALUES(s.portfolio_id,s.instrument_id,0);
        SELECT id INTO asset_value FROM assets WHERE portfolio_id=p_portfolio AND instrument_id=instrument_value;
      END IF;
      moment_value:=date_value(r.moment);
      INSERT INTO transactions(portfolio_id,asset_id,import_batch_id,type,trade_date,quantity,price,gross_amount,currency,fee_amount,tax_amount,operation_order)
        VALUES(p_portfolio,asset_value,p_batch,r.kind,moment_value,r.qty,r.price,r.amount,r.currency,r.fee,0,r.operation_order);
    END LOOP;
    pkg_ledger.recalculate(p_user,p_portfolio);
  END;
  PROCEDURE check_rows(p_batch RAW) IS
    n NUMBER; json_value CLOB; error_value VARCHAR2(2000);
  BEGIN
    FOR r IN (SELECT id,raw_data FROM import_rows WHERE batch_id=p_batch ORDER BY row_number) LOOP
      json_value:=NULL; error_value:=NULL;
      BEGIN normalize(DBMS_LOB.SUBSTR(r.raw_data,8000,1),json_value);
      EXCEPTION WHEN OTHERS THEN
        IF SQLCODE=-20600 THEN error_value:=SUBSTR(SQLERRM,1,2000); ELSE RAISE; END IF;
      END;
      UPDATE import_rows SET parsed_data=json_value,validation_status=CASE WHEN error_value IS NULL THEN 'Valid' ELSE 'Invalid' END,
        error_message=error_value WHERE id=r.id;
    END LOOP;
    SELECT COUNT(*) INTO n FROM import_rows WHERE batch_id=p_batch;
    IF n=0 THEN invalid('Empty batch'); END IF;
    SELECT COUNT(*) INTO n FROM import_rows WHERE batch_id=p_batch AND (validation_status<>'Valid' OR parsed_data IS NULL);
    IF n>0 THEN invalid('Correct invalid rows before validation'); END IF;
  END;
  PROCEDURE validate_batch(p_token VARCHAR2,p_batch_id RAW) IS
    user_value RAW(16); portfolio_value RAW(16); error_value VARCHAR2(2000);
  BEGIN
    SAVEPOINT csv_api; user_value:=actor(p_token); lock_batch(user_value,p_batch_id,portfolio_value); check_rows(p_batch_id);
    SAVEPOINT csv_simulation;
    BEGIN apply_rows(user_value,p_batch_id,portfolio_value);
    EXCEPTION WHEN OTHERS THEN
      IF SQLCODE BETWEEN -20999 AND -20000 OR SQLCODE=-1 THEN error_value:=SUBSTR(SQLERRM,1,1800); ELSE RAISE; END IF;
    END;
    ROLLBACK TO csv_simulation;
    IF error_value IS NOT NULL THEN
      UPDATE import_rows SET validation_status='Invalid',error_message='Batch history validation failed; offending row not isolated: '||error_value WHERE batch_id=p_batch_id;
      UPDATE import_batches SET status='Preview',rejected_count=(SELECT COUNT(*) FROM import_rows WHERE batch_id=p_batch_id) WHERE id=p_batch_id;
    ELSE UPDATE import_batches SET status='Validated',rejected_count=0 WHERE id=p_batch_id; END IF;
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO csv_api; RAISE;
  END;
  PROCEDURE commit_batch(p_token VARCHAR2,p_batch_id RAW) IS
    user_value RAW(16); portfolio_value RAW(16); n NUMBER;
  BEGIN
    SAVEPOINT csv_api; user_value:=actor(p_token); lock_batch(user_value,p_batch_id,portfolio_value); check_rows(p_batch_id);
    apply_rows(user_value,p_batch_id,portfolio_value);
    SELECT COUNT(*) INTO n FROM import_rows WHERE batch_id=p_batch_id;
    UPDATE import_batches SET status='Imported',imported_count=n,rejected_count=0 WHERE id=p_batch_id;
    INSERT INTO audit_log(user_id,action,timestamp,metadata_json) VALUES(user_value,'CsvImported',SYSTIMESTAMP,
      JSON_OBJECT('batch_id' VALUE RAWTOHEX(p_batch_id),'portfolio_id' VALUE RAWTOHEX(portfolio_value),'row_count' VALUE n RETURNING CLOB));
  EXCEPTION WHEN OTHERS THEN ROLLBACK TO csv_api; RAISE;
  END;
  FUNCTION quoted(p_text VARCHAR2) RETURN VARCHAR2 IS
  BEGIN RETURN '"'||REPLACE(p_text,'"','""')||'"'; END;
  FUNCTION decimal_text(p_number NUMBER) RETURN VARCHAR2 IS
  BEGIN RETURN TO_CHAR(p_number,'TM9','NLS_NUMERIC_CHARACTERS=''.,'''); END;
  PROCEDURE export_transactions(p_token VARCHAR2,p_portfolio_id RAW,p_content OUT CLOB) IS
    user_value RAW(16); line_value VARCHAR2(32767);
    PROCEDURE append_line(p_line VARCHAR2) IS
      text_value VARCHAR2(32767):=p_line||CHR(13)||CHR(10);
    BEGIN DBMS_LOB.WRITEAPPEND(p_content,LENGTH(text_value),text_value); END;
  BEGIN
    user_value:=actor(p_token); lock_portfolio(user_value,p_portfolio_id);
    DBMS_LOB.CREATETEMPORARY(p_content,TRUE); append_line(c_header);
    FOR r IN (SELECT t.*,i.ticker,i.name FROM transactions t LEFT JOIN assets a ON a.id=t.asset_id
      LEFT JOIN instruments i ON i.id=a.instrument_id WHERE t.portfolio_id=p_portfolio_id ORDER BY t.trade_date,t.operation_order,t.id) LOOP
      line_value:=quoted(TO_CHAR(r.trade_date,'YYYY-MM-DD"T"HH24:MI:SS.FF9TZH:TZM'))||','||quoted(r.ticker)||','||quoted(r.name)||','||quoted(r.type)||','||
        quoted(decimal_text(r.quantity))||','||quoted(decimal_text(r.price))||','||quoted(r.currency)||','||quoted(decimal_text(r.fee_amount))||','||
        quoted(decimal_text(r.gross_amount))||','||quoted(decimal_text(r.operation_order));
      append_line(line_value);
    END LOOP;
  EXCEPTION WHEN OTHERS THEN
    IF DBMS_LOB.ISTEMPORARY(p_content)=1 THEN DBMS_LOB.FREETEMPORARY(p_content); END IF; RAISE;
  END;
END pkg_csv;
/
