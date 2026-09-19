# VisionWeave Development Plan

## Goal

Deliver a usable Windows image-workflow editor without coupling Nodify or WPF
objects to the workflow format or execution runtime. Each stage must leave the
`master` branch releasable: it builds, its tests pass, and its public contracts
remain backward-compatible within the current `.vwflow` schema.

## Delivery rules

1. **Contracts before presentation.** A UI interaction may only call an
   Application or Domain command; it never edits a `WorkflowDocument` through
   Nodify-owned state.
2. **One vertical boundary per change.** Finish a complete, tested boundary
   (for example document persistence) before adding adjacent UI features.
3. **No WPF type crosses into the core.** Domain, Application, Persistence,
   Contracts, and OpenCV stay usable in a headless test process.
4. **A native frame always has one proven owner.** Every execution change adds
   a lease-ledger assertion for success, failure, and cancellation where it
   introduces or transfers a frame.
5. **Merge only behind a quality gate.** Every stage requires the full Release
   test suite, format verification, project-document validation, and the stage
   evidence listed below.

## Ordered stages

| Stage | Outcome | Depends on | Completion gate |
| --- | --- | --- | --- |
| 0. Foundation | Layer boundaries, typed graph model, validation, DAG runner, OpenCV adapters, accepted ADRs. | — | Completed on `master`. |
| 1. Pre-WPF contract closure | Executor output validation and versioned `.vwflow` read/write. | Stage 0 | Completed by the `feat/pre-wpf-foundation` integration branch; merge before WPF work begins. |
| 2. Application editing services | Typed parameter validation, document command/history service, selection-safe validation projection, and a file/session service around reader/writer. | Stage 1 | Completed on `master`: headless tests prove add/connect/delete/parameter-edit/undo/redo, invalid input rejection, selection-safe validation, save/load, and recovery behaviour. |
| 3. App composition facilities | DI composition root, typed options validation, safe structured logging, async command/error boundary, theme and localization foundations. | Stage 2 | App starts with an empty document; invalid settings show a safe diagnostic; no App reference leaks into core assemblies. |
| 4. Generic Nodify canvas | Document-to-canvas projection, node/connector templates, pan/zoom/selection, compatible connection gestures, and canvas-to-command commit/rollback. | Stages 2–3 | UI smoke test adds two nodes, creates/rejects a connection, selects/deletes a node, and proves undo/redo restores document and canvas. |
| 5. Inspector and document UX | Generic parameter editors, validation panel, dirty state, open/save/recovery prompts, and read-only unsupported-document presentation. | Stages 2–4 | UI smoke test edits a parameter, observes validation, saves/reopens the document, and refuses edits for read-only documents. |
| 6. First runnable user workflow | File-image source, resize/blur, save-image, preview, run/cancel/status presentation, and work-directory policy. | Stages 3–5 | Golden workflow opens in the editor, runs on a real image, produces a managed preview and saved output, and ends with zero outstanding leases. |
| 7. Product hardening | Typed resource references and port schema snapshots, first migration when needed, deterministic cancellation timing, hosted CI evidence, packaging, and accessibility/theme smoke tests. | Stage 6 | Installer/package starts on a clean Windows host; CI is green; recovery, cancellation, dark/light templates, and first migration are covered. |

## What belongs in each near-term stage

### Stage 1 — merge now

- `feat/executor-result-contract`: reject undeclared outputs, port-type
  mismatches, input-lease reuse, and ambiguous output-lease aliases before
  scheduler publication.
- `feat/vwflow-persistence`: deterministic versioned document write, atomic
  save, recovery copy, tolerant load diagnostics, and read-only handling for
  unsupported schemas.

These changes are deliberately integrated before any canvas implementation:
the UI must display stable diagnostics and save a document whose runtime
outputs have already been validated.

### Stage 2 — next implementation sequence

1. Resolve PL-2026-006: validate parameter kind, range, options, unknown
   names, and missing required values in the snapshot boundary.
2. Introduce a headless document command/history service. Commands own undo
   data; the UI supplies only user intent and presentation state.
3. Wrap `WorkflowDocumentReader` and `WorkflowDocumentWriter` in an
   Application-facing session service for new/open/save/autosave/recovery.
4. Keep PL-2026-011's preserved resource and port-schema fragments opaque
   until the first missing-node UI requires their typed model. Do not invent a
   migration framework before a second schema version exists.

### Stages 3–5 — WPF in the right order

Start with a generic node template and connector template, not specialized
OpenCV controls. The projection owns the mapping between stable document IDs
and Nodify view-model items. Nodify gestures create intents; application
commands either commit the document revision and refresh the projection or
return diagnostics and restore the prior visual state. Property editing uses
the same command/history service as the canvas.

Only after this loop is demonstrated should the app add preview panes,
specialized parameter editors, automatic layout, subgraphs, or plugin UI.

### Stages 6–7 — usable product, then expansion

The first user-visible workflow is intentionally narrow: load image → resize
or blur → save image. It proves file references, parameter editing, execution,
preview ownership, cancellation, output ownership, and persistence together.
Batch processing, automatic layout, nested graphs, OCR, calibration, external
plugins, and result caching remain deferred until this vertical slice is
stable.

## Branch policy for the plan

- Keep one short-lived integration branch only while it combines independently
  verified work required by the next stage; here it is
  `feat/pre-wpf-foundation`.
- Merge that branch through one PR into `master`, then delete its source and
  the absorbed feature branches.
- Start WPF work only from the resulting `master`, using stage-scoped branches
  such as `feat/application-editing-services` and `feat/nodify-canvas-mvp`.
- Do not create a long-running UI branch that also accumulates persistence,
  OpenCV, or plugin changes.

## Evidence map

| Concern | Primary evidence |
| --- | --- |
| Architecture | `VisionWeave.ArchitectureTests` |
| Graph, validation, command history, scheduler | `VisionWeave.Domain.Tests` and `VisionWeave.Application.Tests` |
| OpenCV, lease lifetime, golden workflows | `VisionWeave.IntegrationTests` |
| File format and recovery | `VisionWeave.Persistence.Tests` and persistence integration tests |
| WPF interaction | Dedicated UI smoke tests from Stage 4 onward |
| Documentation/format | `scripts/Test-ProjectDocuments.ps1` and `dotnet format --verify-no-changes` |

## Related records

- [Detailed design](visionweave-detailed-design.md)
- [ADR-0004: workflow document and format](../adr/0004-workflow-document-and-format.md)
- [ADR-0005: native resource ownership](../adr/0005-native-resource-ownership.md)
- [ADR-0006: editor and UI commit protocol](../adr/0006-editor-and-ui-commit-protocol.md)
- [ADR-0007: host composition, configuration, and logging stack](../adr/0007-host-composition-and-configuration.md)
- [ADR-0008: async command and error boundary](../adr/0008-async-ui-command-boundary.md)
- [ADR-0009: editor session orchestration](../adr/0009-editor-session-orchestration.md)
- [Project improvements ledger](../ledger/project-improvements.md)
