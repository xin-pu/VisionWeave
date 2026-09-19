# Project Improvements Ledger

## Statuses

`Open`, `Monitoring`, `Implemented`, `Deferred`, or `Closed`.

## Entries

### PL-2026-001 - Close execution and persistence contract decisions

- **Status:** Monitoring
- **Recorded on:** 2026-09-18
- **Scope:** Port values, executable snapshots, native resource ownership, and `.vwflow` persistence.
- **Observation:** The initial skeleton could enforce assembly boundaries, but implementation would have been premature before the design-review P0 items had durable decisions.
- **Decision or next step:** ADR-0003 to ADR-0006 record those decisions and are implemented on the foundation branch. Their status stays Proposed until the maintainer accepts them after review.
- **Evidence:** `docs/adr/0003-port-value-types.md`, `docs/adr/0004-workflow-document-and-format.md`, `docs/adr/0005-native-resource-ownership.md`, `docs/adr/0006-editor-and-ui-commit-protocol.md`.
- **Owner:** VisionWeave maintainers.
- **Review again:** When the maintainer accepts or amends the ADRs after review.

### PL-2026-002 - Add reproducible hosted CI

- **Status:** Monitoring
- **Recorded on:** 2026-09-18
- **Scope:** GitHub pull-request verification.
- **Observation:** A Windows CI workflow is included with the skeleton but has not yet run on GitHub.
- **Decision or next step:** Confirm the first hosted run restores, builds, tests, formats, and validates project documents.
- **Evidence:** `.github/workflows/ci.yml`.
- **Owner:** VisionWeave maintainers.
- **Review again:** After the first pull request.

### PL-2026-003 - Deferred Aries capabilities

- **Status:** Monitoring
- **Recorded on:** 2026-09-19
- **Scope:** First-release capability coverage against the Aries reference implementation.
- **Observation:** Aries offered batch image flow over `Mats`, automatic graph layout and edge routing, nested subgraph nodes, template matching, circle-grid calibration, OCR, and work-directory output export. The first release defers batch/collection flow (ADR-0003), automatic layout and subgraphs (ADR-0006), and the remaining node families (detailed design section 8).
- **Decision or next step:** Keep the deferrals as decisions rather than omissions, and re-evaluate after the first-release node catalog and the persistence path are working end to end.
- **Evidence:** `docs/adr/0003-port-value-types.md`, `docs/adr/0006-editor-and-ui-commit-protocol.md`, `docs/design/visionweave-detailed-design.md`.
- **Owner:** VisionWeave maintainers.
- **Review again:** Before the first-release node catalog is frozen.

### PL-2026-004 - Execution result cache

- **Status:** Deferred
- **Recorded on:** 2026-09-19
- **Scope:** Reuse of node results between runs, including the cache budget and eviction of ADR-0005.
- **Observation:** The run executes every scheduled node and releases every frame it published when the run ends, so nothing is reused. A node whose producer is not part of the plan is therefore reported blocked (`VW-EXEC-003`) instead of running on a stale value. `ExecutionOptions.CacheBudgetBytes` and the diagnostic `VW-EXEC-006` are declared but unreferenced until the cache exists.
- **Decision or next step:** Defer the cache to the slice that implements incremental re-execution of a changed subgraph, and keep `IExecutionInputSource` as the only seam it plugs into so that no executor, node definition, or plan contract changes when it arrives.
- **Evidence:** `src/VisionWeave.Application/Execution/IExecutionInputSource.cs`, `src/VisionWeave.Application/Execution/ExecutionOptions.cs`, `src/VisionWeave.Contracts/Diagnostics/DiagnosticCodes.cs`.
- **Owner:** VisionWeave maintainers.
- **Review again:** When the editor wires incremental runs to a plan built from changed nodes.

### PL-2026-005 - File-backed input and output nodes

- **Status:** Open
- **Recorded on:** 2026-09-19
- **Scope:** The Input/Output node family of the first-release catalog: the image source and save-image nodes.
- **Observation:** The OpenCV layer now ships Gaussian blur and resize, so a native frame can be produced, transformed, previewed, and released by a real run, but the catalog has no node that reads or writes a file. The tests therefore supply frames through a test-only source definition, and no user workflow can start from a real image yet.
- **Decision or next step:** Add the file-backed source and save nodes together with the path-parameter kind and the work-directory rules the design defers, so that a saved workflow can be run end to end from the editor.
- **Evidence:** `src/VisionWeave.OpenCv/Nodes/OpenCvNodeDefinitionProvider.cs`, `tests/VisionWeave.IntegrationTests/Support/NativeWorkflow.cs`, `docs/design/visionweave-detailed-design.md` (section 8).
- **Owner:** VisionWeave maintainers.
- **Review again:** Before the first-release node catalog is frozen.
