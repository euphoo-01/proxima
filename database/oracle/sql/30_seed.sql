SET DEFINE OFF
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
INSERT INTO currencies (code, name, symbol) VALUES ('USD', 'US Dollar', '$');
INSERT INTO currencies (code, name, symbol) VALUES ('EUR', 'Euro', UNISTR('\20AC'));
INSERT INTO currencies (code, name, symbol) VALUES ('BYN', 'Belarusian Ruble', 'Br');
INSERT INTO instruments (id,ticker,name,type,currency,exchange) VALUES
 (HEXTORAW('00000000000000000000000000000001'),'AAPL','Apple Inc.','Stock','USD','NASDAQ');
INSERT INTO instruments (id,ticker,name,type,currency,exchange) VALUES
 (HEXTORAW('00000000000000000000000000000002'),'MSFT','Microsoft Corporation','Stock','USD','NASDAQ');
INSERT INTO instruments (id,ticker,name,type,currency,exchange) VALUES
 (HEXTORAW('00000000000000000000000000000003'),'SPY','SPDR S&P 500 ETF Trust','ETF','USD','NYSE Arca');
INSERT INTO instruments (id,ticker,name,type,currency) VALUES
 (HEXTORAW('00000000000000000000000000000004'),'BTC','Bitcoin','Cryptocurrency','USD');
COMMIT;
