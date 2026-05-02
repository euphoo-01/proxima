# Codex Prompt 06 — Functionality After Visual Parity

Используй `$proxima-feature-implementation`.

Цель: после восстановления Figma parity качественно довести функциональность.

Не начинай эту фазу, пока:
- все core screen mappings заполнены;
- rebuilt screens pass style guard;
- legacy `MainWindow.axaml` больше не является основным местом UI;
- visual deviations are documented.

Приоритет функциональности:
1. PostgreSQL EF repositories replace JSON repositories.
2. Real EF migrations instead of raw SQL bootstrap as primary path.
3. Real ViewModel composition/DI instead of `CreateDefaultViewModel` in code-behind.
4. CSV import finalization and manual import validation.
5. PDF import pipeline/fallback quality.
6. Quote provider and offline cache behavior.
7. Tax draft accuracy boundaries and disclaimers.
8. Reporting/PDF export quality.
9. xUnit/NUnit test migration and Avalonia headless UI tests.

Each task must be one module/feature only.
