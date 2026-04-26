# Proxima — Codex Handoff Pack

Набор файлов для GPT-5.5 Codex агента, который должен собрать рабочее desktop-приложение **Proxima** на **Avalonia UI + C#/.NET + PostgreSQL**.

## Главные вводные

- Приложение называется **Proxima**. Не использовать старое название WealthVision в UI, namespace, заголовках, README, installer metadata.
- Стек: **Avalonia UI**, **C#**, **.NET LTS**, **локальный PostgreSQL**.
- UI: **светлый bento UI**, максимально близкий к Figma-макету.
- Figma reference: `https://www.figma.com/design/Drxcen3JN69XP0fnYxkgOi/Proxima-2?node-id=62-497&p=f&t=5bNwquv4cza52Z5Z-0`
- Данные финансового характера должны храниться локально, безопасно и не отправляться на сторонние серверы без явного действия пользователя.
- Онлайн-запросы разрешены только для котировок, курсов валют, обновлений справочников и Google Drive sync encrypted snapshots.
- Разработка ведется через Git. Каждая итерация фиксируется отдельным conventional commit.

## Порядок чтения агентом

1. `00_MASTER_PROMPT.md`
2. `01_PRODUCT_CONTEXT.md`
3. `02_REQUIREMENTS_SPEC.md`
4. `03_DESIGN_SYSTEM_BENTO_AVALONIA.md`
5. `04_FIGMA_MCP_PROTOCOL.md`
6. `05_ARCHITECTURE_AVALONIA_CSHARP.md`
7. `06_DATABASE_POSTGRES.md`
8. `07_SECURITY_PRIVACY.md`
9. `08_FEATURE_SPECS.md`
10. `09_GIT_WORKFLOW.md`
11. `10_ENGINEERING_STANDARDS.md`
12. `11_ACCEPTANCE_CHECKLIST.md`
13. `12_IMPLEMENTATION_PLAN.md`
14. `13_MODULE_US_AC_TESTS.md`
15. `14_ITERATION_QUALITY_GATE.md`

## Definition of Done

Проект считается готовым, если:

- `dotnet build` проходит без ошибок.
- `dotnet test` проходит.
- Приложение запускается локально.
- Есть миграции БД и seed/demo data.
- Основные экраны реализованы в Avalonia, а не в web wrapper.
- Реализованы: login/unlock, dashboard, all assets, asset details, goals, taxes, settings, import flow.
- Есть локальный PostgreSQL dev setup.
- Нет секретов в репозитории.
- Git history состоит из понятных Conventional Commits.


## Updated Iteration Rule

Каждая итерация должна быть модульной:

```text
Модуль → User Stories → Acceptance Criteria → Tests → Test Run → Fixes → Conventional Commit
```

Подробная матрица модулей находится в `13_MODULE_US_AC_TESTS.md`, а обязательный quality gate — в `14_ITERATION_QUALITY_GATE.md`.
