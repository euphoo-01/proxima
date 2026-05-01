# API Providers Used in Proxima

This document defines external API targets for market quotes and currency exchange rates.

## Currency Exchange Rates (BYN-focused)

- Provider: **Belarusbank Information API**
- Reference: <https://belarusbank.by/o-banke/for-developers/informatsionnye-api/api_kursy_valyut/>
- Usage target:
  - Conversion support for tax calculations and currency-normalized analytics.
  - Source/date must be shown in UI for transparency.

## Market Quotes

- Provider: **Finnhub API**
- Reference: <https://finnhub.io/docs/api/introduction>
- Usage target:
  - Last-price quote refresh for portfolio assets.
  - API key is entered by user in `Settings` and must be stored only via local protected path.
  - UI must never show full key after save (masked only).

### Current implementation status

- `ConfigurableQuoteProvider` selects provider by persisted local settings.
- `FinnhubQuoteProvider` calls `GET /api/v1/quote` and maps `c/o/h/l/t` to app quote model.
- Fallback behavior:
  - If provider is `Mock`, or settings are absent, mock provider is used.
  - If Finnhub key is missing, user-visible refresh failure is returned and existing quote cache fallback is used by refresh flow.

## FX/Tax Exchange Rates

- `ConfigurableExchangeRateProvider` selects provider by persisted local settings.
- `BelarusbankExchangeRateProvider` calls:
  - `https://belarusbank.by/api/kursExchange?city=Минск`
- Adapter currently maps `USD/EUR/RUB` rates against `BYN` and computes cross-rates via BYN.
- If Belarusbank request fails, tax calculations fall back to mock provider with visible source/status in UI.

## Privacy/Security Constraints

- Do not log API keys.
- Do not send portfolio transactions to provider APIs unless explicitly required by a quote/rate request.
- Keep local-first mode with mock/cache fallback when provider config is missing.
