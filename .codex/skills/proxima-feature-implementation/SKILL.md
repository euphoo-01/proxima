---
name: proxima-feature-implementation
description: Use after Figma visual parity is restored, when implementing or hardening Proxima business functionality across Clean Architecture layers.
---

# Workflow

1. Identify the module and user stories from `.codex/handoff/13_MODULE_US_AC_TESTS.md`.
2. Keep implementation in the correct Clean Architecture layer.
3. Do not place SQL, HTTP calls, cryptography internals, or financial formulas in ViewModels.
4. Add or update tests.
5. Run build/test commands.
6. Update `.codex/iteration-log.md` and `.codex/known-limitations.md`.
7. Commit using Conventional Commits.

# Layer rules

- Domain: entities/value objects/domain invariants only.
- Application: use cases, interfaces, validators.
- Infrastructure: EF Core, PostgreSQL, external APIs, filesystem, crypto implementations.
- Analytics: financial calculations.
- Importing: CSV/PDF import pipeline.
- Reporting: PDF/report generation.
- Sync: encrypted snapshot sync.
- App: Avalonia composition, views, view models.

# Feature work must not start if

- current screen lacks Figma mapping;
- style guard fails for the screen being touched;
- there are unrecorded UI parity blockers.
