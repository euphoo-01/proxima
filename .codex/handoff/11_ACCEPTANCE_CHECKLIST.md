# Acceptance Checklist

## Build & Run

- [ ] Repository opens cleanly.
- [ ] `dotnet restore` succeeds.
- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` succeeds.
- [ ] App launches.
- [ ] PostgreSQL can be started locally.
- [ ] Migrations apply.
- [ ] Seed data loads.

## Git

- [ ] Repository initialized.
- [ ] Multiple meaningful commits exist.
- [ ] Commits follow Conventional Commits.
- [ ] Working tree clean or explained.
- [ ] `.gitignore` excludes secrets/build artifacts.

## Avalonia UI

- [ ] App uses Avalonia UI, not web wrapper.
- [ ] Sidebar exists on authenticated pages.
- [ ] Topbar exists on authenticated pages.
- [ ] Login screen exists.
- [ ] Bento UI components are reusable.
- [ ] Light trustworthy design.
- [ ] UI is close to Figma or limitation documented.
- [ ] No business logic in AXAML code-behind.

## Screens

- [ ] Login/Unlock.
- [ ] Dashboard.
- [ ] All Assets.
- [ ] Manual Import.
- [ ] Asset Details.
- [ ] Goals.
- [ ] Taxes.
- [ ] Settings.

## Data

- [ ] Users/profile stored.
- [ ] Portfolios stored.
- [ ] Assets stored.
- [ ] Transactions stored.
- [ ] Prices stored/cached.
- [ ] Goals stored.
- [ ] Tax reports/drafts stored.
- [ ] Import sessions stored.

## Functional

- [ ] Create/switch portfolio.
- [ ] CRUD assets.
- [ ] CRUD transactions.
- [ ] CSV import works.
- [ ] Failed import routes to manual import.
- [ ] Dashboard recalculates.
- [ ] Goals forecast recalculates.
- [ ] Tax cards show draft data.
- [ ] Settings can change base currency/language at least in model.

## Security

- [ ] Local password required.
- [ ] Password not plaintext.
- [ ] No secrets committed.
- [ ] Logs redacted.
- [ ] Sync snapshot encrypted if implemented.
- [ ] Raw DB not synced through Drive.
- [ ] Financial data not sent to third parties except quote/currency lookups.

## Architecture

- [ ] Clean Architecture projects exist.
- [ ] Domain has no infrastructure/UI dependencies.
- [ ] Application uses interfaces.
- [ ] Infrastructure implements interfaces.
- [ ] App composes dependencies.
- [ ] Analytics separated.
- [ ] Importing separated.
- [ ] Reporting separated if implemented.

## Tests

- [ ] Analytics tests.
- [ ] Import tests.
- [ ] Auth/security tests.
- [ ] Application service tests.
- [ ] DB integration tests if possible.

## Documentation

- [ ] README setup.
- [ ] Figma inspection doc.
- [ ] Known limitations.
- [ ] Architecture notes.
- [ ] Security notes.


## Per-Module Iteration Gate

- [ ] Every implemented module references User Stories from `13_MODULE_US_AC_TESTS.md`.
- [ ] Every implemented module has Acceptance Criteria status documented.
- [ ] Every implemented module has tests added or explicitly justified as manual.
- [ ] Every implemented module has test execution evidence.
- [ ] Every module iteration ends with Conventional Commit.
- [ ] `docs/iteration-log.md` contains module-by-module evidence.
