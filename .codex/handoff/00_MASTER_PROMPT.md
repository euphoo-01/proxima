# MASTER PROMPT ДЛЯ GPT-5.5 CODEX AGENT

Ты — senior/principal software engineer, desktop application architect и UI implementation specialist. Твоя задача: за одну рабочую сессию собрать production-oriented MVP приложения **Proxima**.

## Критические требования

1. **Название продукта:** Proxima.
2. **Технологический стек:** Avalonia UI + C# + .NET LTS + PostgreSQL.
3. **Запрещено:** React, Vue, Nuxt, Electron, Tauri, web-first приложение, mock-only prototype без доменной логики.
4. **UI-стиль:** светлый современный **bento UI**, чистый дизайн, карточная сетка, крупные акценты, читаемые графики, много воздуха, доверительная финансовая эстетика.
5. **Figma-first:** перед реализацией UI обязательно изучи Figma-макет через доступный Figma MCP.
6. **Архитектура:** Clean Architecture + MVVM + SOLID + DRY + KISS + YAGNI.
7. **БД:** локальный PostgreSQL, миграции, сиды, транзакционная целостность.
8. **Безопасность:** локальный пароль, защита доступа, шифрование чувствительных данных/backup snapshots, отсутствие утечек в логи и сеть.
9. **Git:** каждая смысловая итерация завершается conventional commit.
10. **Качество:** тесты, форматирование, analyzers, nullable reference types, понятный README.

## Figma source of truth

Figma URL:

`https://www.figma.com/design/Drxcen3JN69XP0fnYxkgOi/Proxima-2?node-id=62-497&p=f&t=5bNwquv4cza52Z5Z-0`

Файл: `Drxcen3JN69XP0fnYxkgOi`  
Начальный node id: `62:497`

Используй Figma MCP для извлечения:

- структуры страниц/фреймов;
- реальных размеров, отступов, сеток, радиусов, цветов, типографики;
- screenshots/reference images;
- компонентных паттернов;
- naming convention слоев;
- визуального ритма bento-grid.

Если Figma MCP недоступен, **не выдумывай точный дизайн**. Реализуй fallback по `03_DESIGN_SYSTEM_BENTO_AVALONIA.md`, но оставь в README явный пункт: “Figma MCP unavailable, UI approximated from spec”.

## Цель результата

Создать репозиторий Avalonia desktop app, который можно запустить локально и показать как рабочее приложение:

- пользователь может войти/разблокировать локальное приложение;
- создать/выбрать портфель;
- увидеть dashboard;
- добавить/импортировать активы;
- открыть детальную страницу актива;
- увидеть базовые и продвинутые метрики;
- создать финансовую цель и прогноз;
- увидеть налоговую страницу для РБ с расчетными карточками;
- открыть настройки;
- данные сохраняются в PostgreSQL;
- демо-данные доступны сразу после первого запуска.

## Приоритеты реализации

### P0 — обязательно

- Avalonia shell: Sidebar + Topbar + content region.
- Navigation: Login, Dashboard, Assets, Asset Details, Goals, Taxes, Settings.
- Локальный PostgreSQL + EF Core/Npgsql + migrations + seed.
- Clean Architecture solution structure.
- MVVM без бизнес-логики в code-behind.
- Bento UI компоненты: `BentoCard`, `MetricCard`, `ChartCard`, `DataTableCard`, `StatusPill`.
- CRUD портфелей, активов, транзакций, целей.
- Dashboard calculations: total value, 24h delta, allocation by tags/categories, latest transactions.
- Import flow: drag&drop modal + CSV parser + PDF placeholder interface + manual import fallback.
- Quote provider abstraction with cache and mock provider.
- Security gate: password setup/unlock, password hash, no plaintext password.
- Git commits by Conventional Commits.

### P1 — желательно

- Candlestick chart for asset details.
- Portfolio report PDF export.
- Tax report PDF export.
- Google Drive encrypted snapshot sync.
- Real quote provider integrations.
- Real NBRB currency provider integration.
- Avalonia.Headless UI tests.

### P2 — можно оставить как extensibility stubs

- Full PDF broker parser for many broker formats.
- Full direct Google Drive OAuth sync.
- Full tax law engine with verified legal references.
- Advanced market metrics requiring paid APIs.

## Нельзя делать

- Нельзя хранить пароль в plaintext.
- Нельзя отправлять транзакции/активы на сторонний сервер.
- Нельзя синхронизировать raw PostgreSQL data directory через Google Drive.
- Нельзя смешивать UI, domain logic, DB access и API clients в одном классе.
- Нельзя hardcode-ить текущие налоговые правила как вечную истину.
- Нельзя делать один огромный commit.
- Нельзя игнорировать ошибки import/parser/quotes; должны быть user-visible error states.
- Нельзя оставлять TODO вместо критического P0 functionality.

## Ожидаемая структура solution

```text
Proxima/
  src/
    Proxima.App/                 # Avalonia UI app
    Proxima.Domain/              # Entities, ValueObjects, Domain services
    Proxima.Application/         # Use cases, DTOs, interfaces, validators
    Proxima.Infrastructure/      # EF Core, PostgreSQL, external APIs, file system, crypto
    Proxima.Reporting/           # PDF/report generation
    Proxima.Importing/           # CSV/PDF import pipeline
    Proxima.Analytics/           # Portfolio and asset metrics
    Proxima.Sync/                # encrypted snapshot sync abstractions
  tests/
    Proxima.Domain.Tests/
    Proxima.Application.Tests/
    Proxima.Infrastructure.Tests/
    Proxima.App.Tests/
  docs/
  scripts/
  docker-compose.yml
  Directory.Build.props
  .editorconfig
  README.md
```


## Обязательный модульный цикл разработки

Каждая итерация реализует один модуль из `13_MODULE_US_AC_TESTS.md`.

Итерация завершается только после:

1. проверки соответствия User Stories;
2. проверки каждого Acceptance Criterion;
3. добавления/обновления тестов для модуля;
4. выполнения тестов;
5. исправления регрессий;
6. обновления `docs/iteration-log.md`;
7. conventional commit.

Запрещено переходить к следующему модулю, если текущий модуль не прошел quality gate из `14_ITERATION_QUALITY_GATE.md`.

## После каждой итерации

Выполни:

```bash
dotnet format
dotnet build
dotnet test
git status
git add .
git commit -m "<type>(<scope>): <summary>"
```

Если тесты падают — сначала исправь, затем коммить. Если необходимо сделать промежуточный WIP commit, используй `chore(wip): ...`, но перед финалом перепроверь состояние.

## Финальный ответ агента

В конце работы выведи:

- что реализовано;
- как запустить;
- какие команды выполнить для БД;
- тестовый логин/пароль для seed/demo mode;
- какие ограничения остались;
- список коммитов;
- known issues.
