# Iteration Quality Gate

## Mandatory Rule

An iteration is complete only when the implemented module satisfies:

```text
User Story alignment + Acceptance Criteria + Tests + Build + Commit
```

The agent must not move to the next module if the current one has untested critical behavior.

## Required Iteration Flow

For every module:

1. **Select module**
   - Pick one module from `13_MODULE_US_AC_TESTS.md`.
   - Write module name and scope to `docs/iteration-log.md`.

2. **Restate User Stories**
   - Copy or summarize relevant US IDs.
   - Confirm what is in scope for this iteration.

3. **Implement**
   - Domain changes first.
   - Application/use-case changes second.
   - Infrastructure changes third.
   - UI changes fourth.
   - Documentation changes last.

4. **Acceptance Criteria Review**
   - Check every AC item.
   - Mark:
     - done;
     - partial;
     - not done;
     - intentionally deferred.
   - Deferred items require reason and issue/known limitation.

5. **Test Formation**
   - Create or update tests after implementation.
   - Tests must cover the behavior introduced in the same iteration.
   - At minimum, write unit tests for domain/application logic.
   - For persistence modules, write integration tests if feasible.
   - For UI modules, write UI/headless tests or explicit manual smoke steps.

6. **Test Execution**
   - Run targeted test project.
   - Run broader tests if shared code changed.
   - Final command before commit:
     ```bash
     dotnet format
     dotnet build
     dotnet test
     ```

7. **Fix**
   - Fix all failing tests.
   - If a test cannot be automated, document manual verification.

8. **Commit**
   - Commit only after checks.
   - Use Conventional Commit.
   - Add commit hash to iteration log.

## AC Status Format

Use this format in `docs/iteration-log.md`:

```md
### Acceptance Criteria Status

| ID | Status | Evidence |
| --- | --- | --- |
| AC-09.1 | Done | DashboardViewModelTests.TotalValue_ReturnsExpected |
| AC-09.2 | Done | Manual check + DeltaCalculatorTests |
| AC-09.3 | Partial | Chart exists, performance test deferred |
```

## Test Status Format

```md
### Tests

| Test Type | Command/File | Result |
| --- | --- | --- |
| Unit | Proxima.Analytics.Tests | Passed |
| Integration | Proxima.Infrastructure.Tests | Passed |
| UI | Manual smoke: Dashboard navigation | Passed |
```

## Commit Message Format

```text
<type>(<scope>): <summary>
```

Examples:

```bash
feat(dashboard): add portfolio overview cards
test(dashboard): cover allocation and delta calculations
fix(import): reject unsupported transaction types
security(sync): encrypt exported snapshots
docs(iteration): record dashboard acceptance results
```

## Required Evidence Before Moving On

Before moving to the next module, the agent must be able to answer:

- Which user stories did this module implement?
- Which acceptance criteria are done?
- Which criteria are partial or deferred?
- Which tests prove the behavior?
- Which commands were run?
- What commit contains the work?

If any answer is missing, the iteration is not complete.

## Recommended Commit Pattern Per Module

For a complex module, use several commits:

```bash
feat(<module>): add domain model
feat(<module>): add application use cases
feat(<module>): add avalonia views
test(<module>): cover core behavior
docs(<module>): record acceptance status
```

For a small module, one commit is acceptable if it includes tests:

```bash
feat(<module>): implement <feature> with tests
```

## No-Go Conditions

Do not close iteration if:

- `dotnet build` fails.
- Critical tests fail.
- AC table is missing.
- User stories are not referenced.
- No commit was made.
- UI compiles but core behavior is fake.
- Feature works only with hardcoded demo values when real persistence was required.
- Security/privacy requirement is violated.
