# Project Improvements Ledger

## Statuses

`Open`, `Monitoring`, `Implemented`, `Deferred`, or `Closed`.

## Entries

### PL-2026-001 - Close execution and persistence contract decisions

- **Status:** Open
- **Recorded on:** 2026-09-18
- **Scope:** Port values, executable snapshots, native resource ownership, and `.vwflow` persistence.
- **Observation:** The initial skeleton can enforce assembly boundaries, but implementation would be premature before the design-review P0 items have durable decisions.
- **Decision or next step:** Create and approve the listed ADRs before implementing the execution engine or workflow serializer.
- **Evidence:** `docs/design/visionweave-detailed-design.md` and the local design review report.
- **Owner:** VisionWeave maintainers.
- **Review again:** Before execution-engine implementation.

### PL-2026-002 - Add reproducible hosted CI

- **Status:** Monitoring
- **Recorded on:** 2026-09-18
- **Scope:** GitHub pull-request verification.
- **Observation:** A Windows CI workflow is included with the skeleton but has not yet run on GitHub.
- **Decision or next step:** Confirm the first hosted run restores, builds, tests, formats, and validates project documents.
- **Evidence:** `.github/workflows/ci.yml`.
- **Owner:** VisionWeave maintainers.
- **Review again:** After the first pull request.
