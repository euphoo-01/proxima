# Источники рыночных данных для Proxima

Проверено 18 ноября 2026. Область: учебное приложение .NET 10 / Avalonia / Oracle и PL/SQL; текущие цены US stocks/ETF и криптовалют, дневная история для аналитики и ML. Регистрация, покупка подписок, установка инструментов и интеграция не выполнялись.

## Можно ли обернуть API биржи

Да. Для котировок обычно достаточно **публичного market-data API**, а не приватного торгового API с балансами, ордерами и ключами пользователя. Например, Binance специально предоставляет домен `data-api.binance.vision` без аутентификации с `ticker/price`, `ticker/bookTicker`, `klines`, `exchangeInfo`; есть отдельный публичный WebSocket. Это официальный интерфейс, который удобно вызвать из C# через HttpClient. [Binance: Market Data Only](https://developers.binance.me/en/docs/products/spot/faqs/market_data_only).

Недокументированный endpoint фронтенда тоже технически можно изучить и обернуть, если доступ разрешён. Но обёртка не создаёт права доступа, не снимает лимиты и не гарантирует стабильность схемы, cookies или защиты сайта. Приватный authenticated API следует использовать только с разрешёнными собственными credentials; для учебного загрузчика цен он обычно не нужен.

Для Proxima достаточно адаптера провайдера внутри существующего C# приложения: получение → нормализация → пакетная запись в Oracle → чтение локальных данных. Отдельный публичный прокси-сервис сейчас не нужен. Он добавляет хостинг и вопросы распространения данных, не увеличивая исходную квоту. HTTP-загрузку разумно оставить в C#, а хранение, расчёты и агрегаты — в Oracle/PLSQL.

## Сравнение

| Источник | Что подходит | Лимиты и существенные ограничения | Вывод |
|---|---|---|---|
| Alpaca Basic | US stocks и ETF, текущий IEX, исторические bars с 2016 | Бесплатно; 200 historical API calls/min; realtime IEX, WebSocket 30 symbols. Полный realtime SIP платный | Первый официальный кандидат, если аккаунт и нужный feed доступны |
| Binance public market data | Криптовалютные пары одной биржи, текущие цены и дневные OHLCV | Без API key; лимиты взвешенные, доступ проверять из фактической сети | Основной кандидат для криптовалют |
| CoinGecko Demo | Агрегированные криптоцены и метаданные | Текущая pricing: 100 calls/min, 10 000/month; публичная история до 365 дней | Дополнение к Binance, не лучший источник многолетних свечей |
| Yahoo / yfinance | Широкая история акций/ETF и исследовательская загрузка | Неофициальный клиент; нет обещанной договорной квоты или SLA | Резервный учебный импорт с локальным кешем |
| Defeat Beta | Дневной исследовательский датасет Yahoo-origin, запросы к Parquet | Сторонний опубликованный набор; свежесть, покрытие и права отдельно проверять | Кандидат для воспроизводимого offline ML, не live quotes |
| MOEX ISS | Инструменты Московской биржи, свечи, справочники | Публичные market data с задержкой 15 минут; realtime по подписке | Хорошая официальная альтернатива при переходе на российский рынок |
| Finnhub | Кандидат для текущих котировок | Бесплатную дневную историю и текущую квоту нельзя считать подтверждёнными этой проверкой | Не выбирать единственным источником истории без проверки прав ключа |
| Financial Datasets | История и snapshots акций | Актуальная pricing предлагает платные Credits / Build / Scale | Не соответствует требованию бесплатного основного источника |

Источники таблицы: [Alpaca plan comparison](https://docs.alpaca.markets/us/v1.1/docs/about-market-data-api), [Binance REST](https://developers.binance.com/en/docs/products/spot/rest-api), [CoinGecko pricing](https://www.coingecko.com/en/api/pricing), [CoinGecko historical granularity](https://support.coingecko.com/articles/5723073761-what-granularity-do-you-support-for-historical-data), [yfinance README](https://github.com/ranaroussi/yfinance), [Defeat Beta README](https://github.com/defeat-beta/defeatbeta-api), [MOEX interfaces](https://www.moex.com/s3630), [Finnhub pricing](https://finnhub.io/pricing), [Finnhub stock candles](https://finnhub.io/docs/api/stock-candles), [Financial Datasets pricing](https://www.financialdatasets.ai/pricing).

### Alpaca: SIP, IEX и Беларусь

FAQ прямо разрешает historical SIP без подписки, если `end` отстоит минимум на 15 минут; latest endpoints и snapshot с SIP требуют подписки. Поэтому бесплатная схема потенциально удобна: текущий IEX плюс завершённая дневная SIP-история. IEX представляет одну площадку: его объём и иногда цена отличаются от консолидированного рынка. Feed надо указывать явно и сохранять вместе с данными. [Market Data FAQ](https://docs.alpaca.markets/us/docs/market-data-faq).

Но документы о paper-only аккаунте говорят о глобальной регистрации по email и entitlement только на IEX. Это нельзя превращать в гарантию SIP для любого нового аккаунта: следует проверить ответ API на собственном аккаунте. [Paper Trading](https://docs.alpaca.markets/us/v1.4.2/docs/paper-trading).

Текущая страница поддерживаемых стран отсылает в support и не подтверждает Беларусь. Отдельная страница Funding Wallets включает Беларусь в запрет денежных переводов; это ограничение конкретного финансового продукта, а не доказательство запрета любой market-data/paper регистрации. Следовательно, **Alpaca остаётся условной рекомендацией, а доступ из Беларуси не считается подтверждённым**. Не требуется открывать live brokerage account или вносить средства ради проверки учебных данных. [Countries available](https://alpaca.markets/support/countries-alpaca-is-available), [Funding Wallets](https://alpaca.markets/support/funding-wallets-for-broker-api-2).

### Криптовалютная история

Для большого backfill Binance публикует дневные и месячные архивы. Это позволяет загрузить историю отдельным импортом, затем обновлять лишь недостающие дни. В spot-архивах с 2025 timestamps в микросекундах: нельзя автоматически считать их миллисекундами. Это история конкретной площадки и пары, например BTCUSDT, а не единая всемирная BTC/USD цена. [Официальный binance-public-data README](https://github.com/binance/binance-public-data/blob/master/README.md).

CoinGecko удобен для агрегированной цены и метаданных; его volume не следует автоматически считать объёмом дневной свечи Binance. У ключевого Demo остаётся месячный потолок. Анонимный Public API имеет плавающие 5–15 calls/min, поэтому его нельзя рекламировать как безлимитный. [CoinGecko rate-limit support](https://support.coingecko.com/articles/5125303456-what-is-the-rate-limit-for-coingecko-api-public-plan).

### Yahoo и Defeat Beta

yfinance прямо сообщает, что не связан с Yahoo, предназначен для исследования/образования, а права на сами данные определяются условиями Yahoo; README напоминает о personal use. Это полезный готовый исследовательский клиент, но не официальный контракт Yahoo API. В текущем README также указана корректировка цен по умолчанию `auto_adjust=True`: нельзя незаметно смешивать такие ряды с raw OHLC. [README](https://github.com/ranaroussi/yfinance).

Defeat Beta читает опубликованный Hugging Face датасет через DuckDB и кеш диапазонов Parquet/JSON. Заявление автора об устранении scraping/rate-limit проблем относится к этой архитектуре, а не является гарантией безлимитной сети или лицензии на данные. Проверить наличие нужных US ETF, дату последней торговой сессии, corporate-action policy и доступность файлов прежде, чем сделать источник основным. [README и пример price()](https://github.com/defeat-beta/defeatbeta-api).

### Finnhub и Financial Datasets

Finnhub pricing и stock-candles — первичные страницы, однако их динамическое содержимое не извлеклось исследовательским инструментом. Поэтому распространённые цифры «60/min» и утверждение о Premium-only candles здесь **не выдаются за независимо подтверждённые**. До выбора необходимо вручную проверить тариф и доступ ключа к `/stock/candle` для AAPL и SPY. Само наличие SDK-метода не доказывает бесплатного entitlement. [Pricing](https://finnhub.io/pricing), [Stock candles](https://finnhub.io/docs/api/stock-candles).

Актуальная Financial Datasets pricing указывает Credits: $20 за 1000 requests, Build: $200/month; snapshots и история доступны на платных планах. Старые сведения о бесплатных отдельных тикерах нельзя считать текущим универсальным free tier. Покупка не рекомендуется. [Pricing](https://www.financialdatasets.ai/pricing).

## Два предложенных GitHub-инструмента

- **api-anything** изучает запросы фронтенда, сохраняет операции, повторяет HTTP и поддерживает каталог/ремонт. README описывает локальные вызовы, optional sharing, sessions и блокировки; чужая рабочая операция может не работать в другой сети. Это инструмент discovery/replay, а не поставщик лицензированных биржевых данных и не обещание повышенных лимитов. При наличии официального endpoint он добавляет лишний runtime. [README](https://github.com/goodnight000/api-anything), [операционные инструкции проекта](https://github.com/goodnight000/api-anything/blob/main/skills/api-anything/SKILL.md).
- **reverse-api-engineer** захватывает браузерный network traffic/HAR, после чего модель генерирует typed client, включая C#. Для разового исследования разрешённого недокументированного frontend API подходит лучше, чем для постоянной фоновой загрузки. Generated client всё равно требует ручной проверки схем, ошибок и credentials. README предупреждает, что backend Codex запускается без sandbox/approval prompts; инструмент в рамках исследования не запускался. [README](https://github.com/kalil0321/reverse-api-engineer).

Проверены README и доступные текстовые инструкции; это оценка заявленного устройства, не аудит всей реализации и не проверка работы на биржевом сайте. Устанавливать оба инструмента ради Binance или Alpaca не требуется.

## Рекомендация и минимальная проверка

1. Если Alpaca допускает регистрацию и данные для данного региона: **Alpaca для US stocks/ETF + Binance public для crypto**. Проверить IEX latest и SIP daily с `end` не новее 15 минут на AAPL/SPY, доступную глубину и pagination. Использовать только разрешённый аккаунту feed.
2. Если Alpaca недоступен: **Yahoo/Defeat Beta как учебный импорт истории с сохранённым snapshot**, Binance для crypto. Текущие equity quotes считать отдельно проверяемой возможностью Yahoo, с кешем и явной меткой свежести. Не обещать стабильный live equity feed. Если нужен именно официальный источник без аккаунта US-провайдера, обсудить смену рынка на MOEX ISS: он не заменяет набор US stocks/ETF.
3. В существующем C# адаптере предусмотреть provider/feed, instrument mapping, exchange/currency, source timestamp, fetched-at UTC, interval, adjustment mode и pagination. Обрабатывать 429 с указанным Retry-After и backoff; новые запросы делать только для пропусков. UI читает кеш Oracle, а не запрашивает внешний API на каждую перерисовку.

Для ML нужны завершённые daily bars и согласованная корректировка splits/dividends; текущую незакрытую свечу нельзя включать как финальную. Volume свечи — объём торгов за её интервал в определённом feed; latest trade size — размер одной сделки; market cap — капитализация; quote volume — оборот в валюте котировки. Эти поля не взаимозаменяемы. VWAP нельзя восстановить точно из одних OHLC: использовать поле провайдера либо вычислять по сделкам; для Binance bar отношение quote volume/base volume относится лишь к данной паре и площадке. Названия/единицы подтверждать схемой конкретного endpoint, а не общим полем `volume`.
