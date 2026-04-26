# Implementation Plan

## Mandatory Iteration Gate

For every iteration below, the agent must use:

- `13_MODULE_US_AC_TESTS.md` for User Stories, Acceptance Criteria and tests;
- `14_ITERATION_QUALITY_GATE.md` for the required close-out protocol.

No iteration ends after implementation only. It ends after AC review, test creation, test execution, fixes and Conventional Commit.


## Iteration 0 — Repository & Solution

Tasks:

- initialize git;
- create solution;
- add projects;
- configure Directory.Build.props;
- configure .editorconfig;
- add .gitignore;
- add README skeleton.

Commit:

```bash
git commit -m "chore(repo): initialize proxima solution"
```

## Iteration 1 — Figma Inspection

Tasks:

- use Figma MCP;
- inspect source design;
- export screenshots;
- document tokens.

Commit:

```bash
git commit -m "docs(figma): document inspected design tokens"
```

## Iteration 2 — Avalonia Theme & Controls

Tasks:

- add Tokens.axaml;
- add typography/button/input/card styles;
- create BentoCard/MetricCard/StatusPill;
- create shell layout.

Commit:

```bash
git commit -m "feat(theme): add bento ui design system"
```

## Iteration 3 — Navigation & Shell

Tasks:

- Login shell route;
- authenticated shell;
- sidebar;
- topbar;
- navigation service.

Commit:

```bash
git commit -m "feat(app): add shell navigation"
```

## Iteration 4 — Database

Tasks:

- EF Core context;
- entities;
- configurations;
- migrations;
- docker-compose;
- seed service.

Commit:

```bash
git commit -m "feat(db): add postgres persistence model"
```

## Iteration 5 — Auth

Tasks:

- local profile setup;
- password hashing;
- unlock flow;
- session state.

Commit:

```bash
git commit -m "security(auth): add local unlock flow"
```

## Iteration 6 — Portfolio / Assets / Transactions

Tasks:

- services;
- repositories;
- ViewModels;
- CRUD UI.

Commit:

```bash
git commit -m "feat(portfolio): add assets and transactions"
```

## Iteration 7 — Import

Tasks:

- drag&drop modal;
- CSV parser;
- import validation;
- manual import fallback.

Commit:

```bash
git commit -m "feat(import): add csv import with manual fallback"
```

## Iteration 8 — Dashboard

Tasks:

- valuation service;
- allocation service;
- dashboard ViewModel;
- chart/table/cards.

Commit:

```bash
git commit -m "feat(dashboard): add portfolio overview"
```

## Iteration 9 — Asset Details

Tasks:

- detail route;
- OHLC chart;
- metric calculations;
- transaction table.

Commit:

```bash
git commit -m "feat(asset-details): add analytics view"
```

## Iteration 10 — Goals

Tasks:

- goals CRUD;
- forecast service;
- forecast chart.

Commit:

```bash
git commit -m "feat(goals): add compound interest forecast"
```

## Iteration 11 — Taxes

Tasks:

- tax profile;
- draft tax report;
- NBRB provider interface;
- bento tax cards.

Commit:

```bash
git commit -m "feat(taxes): add belarus tax overview draft"
```

## Iteration 12 — Settings & Sync

Tasks:

- settings page;
- profile edit;
- base currency;
- sync settings;
- encrypted snapshot service.

Commit:

```bash
git commit -m "feat(settings): add profile and sync settings"
```

## Iteration 13 — Tests & Hardening

Tasks:

- domain tests;
- application tests;
- import tests;
- security tests;
- run vulnerable package check.

Commit:

```bash
git commit -m "test(core): add coverage for calculations and import"
```

## Iteration 14 — Final Polish

Tasks:

- README complete;
- known limitations;
- screenshots;
- format;
- final build/test.

Commit:

```bash
git commit -m "docs(readme): add setup and release notes"
```
