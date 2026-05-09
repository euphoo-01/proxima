# API providers

## Market data

- Provider: **Twelve Data API**
- Reference: <https://twelvedata.com/docs>
- Runtime integration:
  - `TwelveDataQuoteProvider` calls `GET /quote` and maps `open/high/low/close/volume/timestamp` to the app quote model.
  - `TwelveDataSymbolSearchService` calls `GET /symbol_search` and returns exact provider symbols for stocks, ETF, forex and crypto.
  - `TwelveDataAssetMarketDataProvider` calls `GET /time_series` for OHLCV candles used by the asset details chart.
- Authentication:
  - API key is stored locally in protected settings.
  - Runtime HTTP requests use the `Authorization: apikey <key>` header, so the key is not embedded into request URLs.
- Error policy:
  - If the key is missing, quote refresh/search/chart loading returns a visible user-facing error.
  - No synthetic market data is generated when provider data is unavailable.

## Currency rates

Currency conversion remains isolated behind the currency-provider abstraction and is not coupled to market-data providers.
