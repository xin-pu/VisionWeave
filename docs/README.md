# VisionWeave Documentation

This directory is the English documentation entry point for VisionWeave. New
product-facing design and operating documents should be written in English.
Historical source material may remain in its original language when translating
it would add noise without changing a current decision.

## Start here

- [Design documents](design/README.md) — delivery plans, UI readiness, and
  detailed product design.
- `adr/` — accepted architecture decision records and their rationale; start
  with [ADR-0009](adr/0009-editor-session-orchestration.md) for the editing
  session the shell presents,
  [ADR-0008](adr/0008-async-ui-command-boundary.md) for the shell's command and
  error boundary,
  [ADR-0010](adr/0010-diagnostic-targets.md) for what a diagnostic is attributed
  to, and
  [ADR-0007](adr/0007-host-composition-and-configuration.md) for the current
  application-host direction.
- [Project ledgers](ledger/README.md) — improvements, risks, and standards
  deviations.
- [Development standards reference](standards-reference.md) — the pinned
  external standards baseline used by this repository.

## Required reading by role

| Role | Read before changing the repository |
| --- | --- |
| Any contributor | Root [README](../README.md), standards reference, and the relevant issue. |
| Application or persistence contributor | [Development plan](design/development-plan.md) and the applicable ADRs. |
| WPF or Nodify contributor | [Pre-UI hardening and visual direction](design/pre-ui-hardening-and-visual-direction.md), [ADR-0006](adr/0006-editor-and-ui-commit-protocol.md), and the current issue's acceptance criteria. |
| Reviewer | The issue, affected ADRs, tests, and the matching completion gate in the development plan. |

## Documentation rules

- Link to the source of a decision instead of duplicating it in several files.
- Keep plans actionable: state dependencies, repository ownership, acceptance
  criteria, and verification commands.
- Record architectural choices in an ADR and unresolved cross-cutting work in
  the project improvements ledger.
- Run `scripts/Test-ProjectDocuments.ps1` after modifying documentation.
