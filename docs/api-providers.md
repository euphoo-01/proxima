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

## Privacy/Security Constraints

- Do not log API keys.
- Do not send portfolio transactions to provider APIs unless explicitly required by a quote/rate request.
- Keep local-first mode with mock/cache fallback when provider config is missing.
