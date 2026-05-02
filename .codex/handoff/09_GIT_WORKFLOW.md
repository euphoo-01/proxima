# Git Workflow & Conventional Commits

## Non-Negotiable Rule

Every meaningful iteration must be committed. Do not produce one giant final commit.

## Before Starting

Run:

```bash
git init
git status
```

Create initial commit:

```bash
git add .
git commit -m "chore(repo): initialize proxima workspace"
```

## Commit Style

Use Conventional Commits:

```text
<type>(<scope>): <summary>
```

Allowed types:

- `feat` — new feature
- `fix` — bug fix
- `refactor` — internal change without behavior change
- `test` — tests
- `docs` — documentation
- `style` — formatting only
- `build` — build system/dependencies
- `ci` — CI
- `chore` — maintenance
- `perf` — performance
- `security` — security hardening

Examples:

```bash
git commit -m "feat(app): add avalonia shell navigation"
git commit -m "feat(db): add portfolio and transaction schema"
git commit -m "feat(dashboard): implement portfolio summary cards"
git commit -m "fix(import): handle empty csv rows safely"
git commit -m "security(auth): hash local password with pbkdf2"
git commit -m "test(analytics): cover roi and max drawdown calculations"
git commit -m "docs(readme): add local setup instructions"
```

## Commit Cadence

Recommended iteration commits:

1. `chore(repo): initialize solution structure`
2. `build(app): add avalonia and shared project references`
3. `feat(theme): add bento design tokens and base controls`
4. `feat(shell): implement sidebar topbar and navigation`
5. `feat(db): add postgres context and initial migrations`
6. `feat(auth): add local profile setup and unlock flow`
7. `feat(portfolios): add portfolio selection and crud`
8. `feat(assets): add asset and transaction management`
9. `feat(import): add csv import and manual fallback`
10. `feat(dashboard): add portfolio metrics and charts`
11. `feat(asset-details): add metrics and price chart screen`
12. `feat(goals): add compound forecast page`
13. `feat(taxes): add tax overview draft page`
14. `feat(settings): add profile and sync settings`
15. `security(sync): add encrypted snapshot export`
16. `test(core): add domain and application tests`
17. `docs(readme): document setup and known limitations`


## Iteration Close Rule

Before every commit that closes a module iteration:

1. Open `13_MODULE_US_AC_TESTS.md`.
2. Check module User Stories.
3. Check module Acceptance Criteria.
4. Add/update tests for the module.
5. Run tests.
6. Update `.codex/iteration-log.md`.
7. Commit.

A module commit without AC/test evidence is not acceptable.

## Required Pre-Commit Checks

Before each non-doc commit:

```bash
dotnet format
dotnet build
dotnet test
```

If time is tight, at minimum:

```bash
dotnet build
```

But final state must pass all checks.

## Branching

For this task, a single branch is acceptable:

```bash
main
```

If using branches:

```bash
git checkout -b feat/proxima-mvp
```

## Git Hygiene

Do not commit:

- `.env`
- real API keys
- raw broker reports
- local PostgreSQL data directory
- user secrets
- build outputs
- `bin/`
- `obj/`
- generated logs

Add `.gitignore` for .NET/Avalonia and local secrets.

## Final Report

Final answer must include:

```bash
git log --oneline --decorate --graph --all
git status --short
```

Explain if working tree is not clean.
