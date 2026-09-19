# Project Improvements Ledger

## Statuses

`Open`, `Monitoring`, `Implemented`, `Deferred`, or `Closed`.

## Entries

### PL-2026-001 - Close execution and persistence contract decisions

- **Status:** Implemented
- **Recorded on:** 2026-09-18
- **Scope:** Port values, executable snapshots, native resource ownership, and `.vwflow` persistence.
- **Observation:** The initial skeleton could enforce assembly boundaries, but implementation would have been premature before the design-review P0 items had durable decisions.
- **Decision or next step:** ADR-0003 to ADR-0006 record those decisions and are implemented on the foundation branch. The maintainer accepted them on 2026-09-19.
- **Update (2026-09-19):** The foundation slice is implemented on `feat/foundation`. Review remediation branches [#1](https://github.com/xin-pu/VisionWeave/issues/1) and [#2](https://github.com/xin-pu/VisionWeave/issues/2) are pushed and closed; the foundation ref itself still awaits review before it is merged to `master`. The corresponding full Release suites passed with 125 and 124 tests respectively, together with format verification.
- **Update (2026-09-19):** With the ADRs accepted, the foundation fast-forwarded into `master` locally, which stays unpushed until the maintainer asks for it. The `.vwflow` persistence half of this scope is not implemented and is tracked separately as PL-2026-007.
- **Evidence:** `docs/adr/0003-port-value-types.md`, `docs/adr/0004-workflow-document-and-format.md`, `docs/adr/0005-native-resource-ownership.md`, `docs/adr/0006-editor-and-ui-commit-protocol.md`.
- **Owner:** VisionWeave maintainers.
- **Review again:** No further review for these decisions; reopen only if a port value, snapshot, or ownership rule changes, and follow `.vwflow` persistence through PL-2026-007.

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

### PL-2026-006 - Parameter values are not validated at the definition boundary

- **Status:** Implemented
- **Priority:** P1
- **Recorded on:** 2026-09-19
- **Scope:** Definition-boundary validation of node parameter values: kind, bounds, options, unknown names, and required values.
- **Observation:** The snapshot now merges each declared `ParameterDefinition.DefaultValue` under the values a document saved, so a node placed without an explicit value reaches the executor with its default. Nothing validates the saved values themselves: a text value where an integer is declared, a number outside `Minimum`/`Maximum`, an option outside `Options`, or a name the definition does not declare all travel through the snapshot unchanged, and an executor re-checks only the values it happens to read. A required parameter that also declares no default is absent from the snapshot, so `NodeParameterSet.GetInt32` throws `KeyNotFoundException` and the run reports it as `VW-EXEC-001` instead of refusing the document up front. `ParameterDefinition.IsRequired`, `Minimum`, `Maximum`, and `Options` are therefore schema data the editor can show but the build does not enforce.
- **Decision or next step:** Validate parameter values where the definition is resolved — in the snapshot factory or a dedicated validator — with diagnostics for a kind mismatch, an out-of-range value, an unknown parameter name, and a missing required value, so an unrunnable document is refused before a snapshot exists. Every OpenCV definition starts from a declared default today, which is why the shipped catalog runs as placed; keep it that way until this validation lands.
- **Update (2026-09-19):** `WorkflowValidator` now checks parameters where it already resolves a node's definition, so a document that reports any parameter diagnostic never becomes a snapshot and no unreadable value reaches an executor. `VW-PARAM-001` reports a value whose shape does not match the declared kind (including a fractional value for an integer parameter), `VW-PARAM-002` a numeric value outside `Minimum`/`Maximum`, `VW-PARAM-003` an option outside the declared set, `VW-PARAM-004` a name the definition does not declare, and `VW-PARAM-005` a required parameter with neither a saved value nor a declared default. A saved `null` counts as unset rather than a kind mismatch, and a disabled node is only shape-checked, because its required values never reach a run.
- **Evidence:** `src/VisionWeave.Application/Validation/WorkflowValidator.cs`, `src/VisionWeave.Contracts/Diagnostics/DiagnosticCodes.cs`, `src/VisionWeave.Application/Execution/WorkflowSnapshotFactory.cs`, `tests/VisionWeave.Application.Tests/Validation/WorkflowValidatorTests.cs`, `tests/VisionWeave.Application.Tests/Execution/WorkflowSnapshotFactoryTests.cs`, `docs/design/visionweave-detailed-design.md` (section 4.3).
- **Owner:** VisionWeave maintainers.
- **Review again:** When the editor writes parameter values into a document, or when a path parameter enters the catalog.

### PL-2026-007 - Deliver the persisted workflow vertical slice

- **Status:** Implemented
- **Priority:** P1
- **Recorded on:** 2026-09-19
- **Scope:** A real `.vwflow` document flowing from persistence into validation, snapshot construction, execution, and recovery diagnostics.
- **Observation:** The Domain and Application layers now distinguish editable documents from executable snapshots, but the Persistence project has no exercised serializer/loader path. Without it, version handling, malformed-document recovery, resource references, and missing-node placeholders remain design-only behavior.
- **Decision or next step:** Implement a versioned serializer and loader with round-trip, malformed-input, forward-version, and missing-definition tests. Keep WPF and Nodify types out of the format so the file remains an application-owned contract.
- **Update (2026-09-19):** The narrow vertical slice is implemented on `feat/vwflow-persistence`. `WorkflowDocumentWriter` writes the versioned format deterministically, saves atomically through a temporary file in the destination directory, and keeps a separate recoverable working copy; `WorkflowDocumentReader` reports `VW-FILE-001` for a file that is not a readable document, `VW-FILE-002` and a read-only result for a schema version this build cannot migrate, and `VW-FILE-003` for an entry or field it had to skip. Unknown fields survive a load and a save verbatim, and a load restores the stored revision and modification instant instead of revising the document, which is why `WorkflowDocument.Restore` became `WorkflowDocument.Hydrate`.
- **Update (2026-09-19):** The document `resources` list and each node's `portSchemaSnapshot` are preserved verbatim as extension fragments but are not modeled yet, and no migration pipeline exists, so an older or newer schema version opens read-only. That narrowing is tracked as PL-2026-011.
- **Evidence:** `src/VisionWeave.Persistence/Workflows/`, `src/VisionWeave.Domain/Workflows/WorkflowDocument.cs`, `tests/VisionWeave.Persistence.Tests/Workflows/`, `tests/VisionWeave.IntegrationTests/Persistence/WorkflowFileExecutionTests.cs`, `docs/adr/0004-workflow-document-and-format.md`.
- **Owner:** VisionWeave maintainers.
- **Review again:** Before the editor writes documents, when a resource reference enters the catalog, or when the first schema migration is required.

### PL-2026-008 - Build the first Nodify editor vertical slice

- **Status:** Open
- **Priority:** P1
- **Recorded on:** 2026-09-19
- **Scope:** WPF UI bindings for document-backed nodes, ports, connections, selection, property editing, undo/redo, and run-state presentation.
- **Observation:** The foundation provides a WPF shell, the Domain document model, and editor commit rules, but it has not yet proven that Nodify interactions become reversible document commands without leaking UI objects into Domain or Application.
- **Decision or next step:** Implement one end-to-end canvas workflow: add nodes, connect compatible ports, edit a parameter, undo/redo each document change, and render validation/run diagnostics. Add a UI smoke test for this flow before expanding node families.
- **Evidence:** `src/VisionWeave.App/`, `docs/adr/0006-editor-and-ui-commit-protocol.md`, `docs/design/visionweave-detailed-design.md` (sections 7 and 10).
- **Owner:** VisionWeave maintainers.
- **Review again:** Before introducing custom node controls, automatic layout, or subgraphs.

### PL-2026-009 - Validate executor results against node output contracts

- **Status:** Implemented
- **Priority:** P1
- **Recorded on:** 2026-09-19
- **Scope:** The boundary between `INodeExecutor` results and scheduler publication.
- **Observation:** The runner publishes every successful `NodeExecutionResult.Outputs` dictionary as supplied. It does not yet reject an unknown output port, a value whose `PortTypeId` conflicts with the declared output, or one lease reused across semantically distinct output ports. Such errors currently surface later as blocked consumers or scheduler failures rather than as a deterministic producer diagnostic.
- **Decision or next step:** Validate result keys, declared output directions, value types, and duplicate image-lease ownership before publication. Define whether a node may intentionally alias one frame to multiple outputs; if allowed, represent that explicitly and reserve it safely.
- **Update (2026-09-19):** `NodeOutputContract` now checks a successful result before the runtime publishes anything, and reports `VW-EXEC-010` for a port the definition does not declare as an output, `VW-EXEC-011` for a missing value or a value whose port type is not the declared one, `VW-EXEC-012` for one lease reported on two output ports, and `VW-EXEC-013` for a lease the node received as an input. Aliasing stays unsupported: an executor publishes leases it created, and a node that needs two outputs produces two frames. Rejected outputs are released unless the node only borrowed them, which also covers the failure and cancellation paths. A successful node that publishes no value for a declared output is deliberately left as a blocked consumer, because a missing value is a schedulable condition rather than a contract violation.
- **Evidence:** `src/VisionWeave.Application/Execution/NodeOutputContract.cs`, `src/VisionWeave.Contracts/Diagnostics/DiagnosticCodes.cs`, `tests/VisionWeave.Application.Tests/Execution/WorkflowRunnerOutputContractTests.cs`, `src/VisionWeave.Application/Execution/WorkflowRunner.cs`, `src/VisionWeave.Application/Execution/RunState.cs`, `src/VisionWeave.Contracts/Execution/NodeExecutionResult.cs`, `docs/adr/0005-native-resource-ownership.md`.
- **Owner:** VisionWeave maintainers.
- **Review again:** Reopen if a node ever needs to publish one frame on several ports or to publish to an optional output port; both need an explicit ownership representation in ADR-0005 rather than a relaxed check.

### PL-2026-010 - Make runtime time and cancellation tests deterministic

- **Status:** Monitoring
- **Priority:** P2
- **Recorded on:** 2026-09-19
- **Scope:** Cancellation grace periods, preview fences, and quarantine paths in the application runtime.
- **Observation:** Current behavior is covered by short real-time delays. The corrected per-level grace wait is now cancelled on normal completion, but its absence cannot be asserted directly, and timing-sensitive tests can become flaky under host load.
- **Decision or next step:** Introduce a narrow, application-owned time/wait abstraction or `TimeProvider` seam for runner waits. Use it to assert normal-completion cleanup, grace expiry, and late executor completion without wall-clock sleeps; keep executor contracts free of test-only clock dependencies.
- **Evidence:** `src/VisionWeave.Application/Execution/WorkflowRunner.cs`, `tests/VisionWeave.Application.Tests/Execution/WorkflowRunnerCancellationTests.cs`, [#1](https://github.com/xin-pu/VisionWeave/issues/1).
- **Owner:** VisionWeave maintainers.
- **Review again:** Before extending quarantine behavior to plugins or adding more timing-dependent execution features.

### PL-2026-011 - Model resource references and typed port schema snapshots

- **Status:** Open
- **Priority:** P1
- **Recorded on:** 2026-09-19
- **Scope:** The two `.vwflow` fields the format defines but the model does not carry — the document `resources` list and each node's `portSchemaSnapshot` — together with the migration pipeline that would let an unsupported schema version load instead of opening read-only.
- **Observation:** The reader preserves both fields verbatim as extension fragments and the writer re-emits them, so a load and a save lose nothing, but nothing can read them either. A resource reference therefore cannot participate in the snapshot fingerprint of ADR-0004 decision 7, and a node whose plugin is absent cannot be rendered from its snapshot, so such a document opens and saves yet shows no placeholder. Every schema version other than the current one opens read-only because no `IWorkflowMigration` is registered.
- **Decision or next step:** Add a typed port schema snapshot to the node instance model and a resource list to the document, emit both under their own top-level fields, and move the preserved fragments into them with the first schema migration. Introduce `IWorkflowMigration` keyed by source schema version, forward-only and idempotent, when the second schema version appears rather than before it.
- **Evidence:** `src/VisionWeave.Persistence/Workflows/WorkflowDocumentReader.cs`, `src/VisionWeave.Persistence/Workflows/WorkflowDocumentWriter.cs`, `docs/adr/0004-workflow-document-and-format.md`, `docs/design/visionweave-detailed-design.md` (section 7).
- **Owner:** VisionWeave maintainers.
- **Review again:** Before the editor renders a placeholder node, or before the snapshot computes a resource fingerprint.

### PL-2026-012 - Deliver the application editing services

- **Status:** Implemented
- **Priority:** P1
- **Recorded on:** 2026-09-19
- **Scope:** The headless editing boundary the canvas will call: a document command/history service that owns undo/redo, a selection-safe validation projection, and an application-facing file/session service around the `.vwflow` reader and writer.
- **Observation:** The document model and the validator are complete, and ADR-0006 requires that a UI interaction becomes an application command which reports its outcome as diagnostics, with a single undo stack that owns the data its own reversal needs. Neither the command history nor a session service for new/open/save/autosave/recovery exists yet, so the editor slice has nothing to call and the persistence layer has no caller that manages an editing session.
- **Decision or next step:** Add the command/history service first, then wrap `WorkflowDocumentReader` and `WorkflowDocumentWriter` in a session service that owns the current file, the dirty state, autosave, and recovery. Keep connection legality in `WorkflowValidator` so no command re-implements a rule.
- **Update (2026-09-19):** The command and history half is implemented. `IDocumentCommand` carries one reversible edit and the data its own reversal needs; `DocumentCommandHistory` owns the single undo stack and returns a `DocumentCommandResult` instead of throwing at the caller. `AddNodeCommand`, `RemoveNodeCommand`, `MoveNodesCommand`, `SetNodeParameterCommand`, `ConnectPortsCommand`, and `DisconnectPortsCommand` cover the canvas intents of ADR-0006. A refused edit changes neither the document nor the history, a semantic edit increments the revision while a move does not, and `SetNodeParameterCommand` coalesces successive edits of one parameter within `DocumentEditingOptions.ParameterCoalescingWindow`. `WorkflowValidator.ValidateConnection` is the single judge of a candidate wire and reports only the diagnostics that wire would introduce, so a pending connection can be judged while the rest of the graph is still incomplete. The session service remains open.
- **Update (2026-09-19):** The session half is implemented as `WorkflowSession` in `Persistence`, which is the only layer that can see both the document and the file; keeping it there leaves the allowed reference table unchanged. It binds a document to its file, carries the load diagnostics forward, refuses to write a read-only document, and reports `IsDirty` from a new `WorkflowDocument.ChangeCount` rather than from the revision, because a layout-only move must make a saved document dirty again. `Save` writes atomically and drops the working copy, `TryAutosave` writes the working copy only for a dirty, writable document with a path, and `Recover` reopens the working copy still bound to the document path so a recovered document saves over the document instead of over the copy. Both halves of this entry are now in place.
- **Update (2026-09-19):** The validation projection is implemented as `ValidationProjection`, an immutable view of one validation outcome indexed by stable node instance identifier and tagged with the revision it was computed from. It answers `DiagnosticsFor`, `SeverityOf`, and `Select` without re-validating and without holding a view model, so a selection that outlives the edit which produced it reports nothing for a node the document no longer contains, `Select` does not depend on the order or the multiplicity of the identifiers it is given, and a selection also carries the document-level diagnostics so a panel cannot call a selection runnable while a graph-wide failure still blocks the run. `Matches` is how the caller tells a current projection from a superseded one; because a layout-only move increments no revision, a node move does not invalidate it. `WorkflowValidator.Project` validates and tags in one call so a projection cannot claim the wrong revision. Port-level and connection-level attribution is still not derivable and is tracked as PL-2026-013.
- **Evidence:** `src/VisionWeave.Application/Editing/`, `src/VisionWeave.Persistence/Workflows/WorkflowSession.cs`, `src/VisionWeave.Persistence/Workflows/WorkflowSessionResult.cs`, `src/VisionWeave.Domain/Workflows/WorkflowDocument.cs` (`ChangeCount`), `src/VisionWeave.Application/Validation/ValidationProjection.cs`, `src/VisionWeave.Application/Validation/SelectionValidation.cs`, `src/VisionWeave.Application/Validation/WorkflowValidator.cs` (`ValidateConnection`, `Project`), `tests/VisionWeave.Application.Tests/Editing/`, `tests/VisionWeave.Application.Tests/Validation/ValidationProjectionTests.cs`, `tests/VisionWeave.Persistence.Tests/Workflows/WorkflowSessionTests.cs`, `docs/design/visionweave-detailed-design.md` (sections 4.3, 6.1, 7, and 10).
- **Owner:** VisionWeave maintainers.
- **Review again:** When the composition root pairs the session with the history, or when a canvas intent needs a command the set does not provide.

### PL-2026-014 - Compose the WPF host and validate its settings

- **Status:** Implemented
- **Priority:** P1
- **Recorded on:** 2026-09-19
- **Scope:** The host composition root: dependency injection for the node catalog, the validator, and the resolved options; settings files bound to typed options and validated at startup; the structured logging stack; and the boundary that keeps the host packages out of the core assemblies.
- **Observation:** The core layers are complete and headless, but nothing composes them. `App` is an empty shell whose window is created by `StartupUri`, no settings file is read, and no project references a dependency-injection, configuration, or logging stack. ADR-0002 named the UI, canvas, OpenCV, and MVVM packages but no host stack, so neither it nor the dependency table said which one to use, and the settings that section 9 describes — execution concurrency, preview limits, cache limits, and the autosave interval — were values only a test could set.
- **Decision or next step:** Add a host stack that only the composition root references, bind each settings section to the options record of the layer that honors it, validate every section at startup and refuse to start on a rejected value, and record the choice and its boundary in an ADR.
- **Update (2026-09-19):** Implemented with ADR-0007. `VisionWeaveSettings` binds `appsettings.json` plus an optional `appsettings.user.json` and validates every section, `VisionWeaveServices` registers the options, the `TimeProvider`, every `INodeDefinitionProvider`, the catalog built from all of them, and the validator, and `App` runs that sequence before it opens a window. Microsoft.Extensions 10.0.12 — DependencyInjection, Configuration.Json, Configuration.Binder, Logging, and Logging.Debug — is referenced by `App` alone, and a new architecture test refuses those packages in every core assembly. Validation lives with each options record (`ExecutionOptions`, `FramePreviewOptions`, `WorkflowAutosaveOptions`) and reports `VW-CONFIG-001`; rejected range values and unreadable, malformed, or unbindable configuration are logged, shown in a modal message, and end startup with exit code 1 instead of being clamped or escaping the startup boundary. Headless `VisionWeave.App.Tests` covers the configuration boundary. The asynchronous command and error boundary of the shell, the theme and localization foundations, and run-boundary logging with `OperationId`, `WorkflowId`, `NodeId`, `NodeTypeId`, `DiagnosticCode`, and elapsed milliseconds remain open in this stage.
- **Evidence:** `src/VisionWeave.App/Composition/`, `src/VisionWeave.App/App.xaml.cs`, `src/VisionWeave.App/ViewModels/MainWindowViewModel.cs`, `src/VisionWeave.App/appsettings.json`, `Directory.Packages.props`, `src/VisionWeave.Application/Execution/ExecutionOptions.cs`, `src/VisionWeave.OpenCv/Preview/FramePreviewOptions.cs`, `src/VisionWeave.Persistence/Workflows/WorkflowAutosaveOptions.cs`, `tests/VisionWeave.Application.Tests/Execution/ExecutionOptionsTests.cs`, `tests/VisionWeave.IntegrationTests/Preview/FramePreviewOptionsTests.cs`, `tests/VisionWeave.Persistence.Tests/Workflows/WorkflowAutosaveOptionsTests.cs`, `tests/VisionWeave.ArchitectureTests/Dependencies/AssemblyDependencyTests.cs`, `docs/adr/0007-host-composition-and-configuration.md`, `docs/design/visionweave-detailed-design.md` (sections 4.3, 9, and 10).
- **Owner:** VisionWeave maintainers.
- **Review again:** When the shell's asynchronous command and error boundary is added, or when a plugin provider joins the catalog.

### PL-2026-013 - Attribute diagnostics to the port or connection they describe

- **Status:** Open
- **Priority:** P2
- **Recorded on:** 2026-09-19
- **Scope:** Port-level and connection-level validation attribution: which port or connection a diagnostic belongs to.
- **Observation:** `NodeDiagnostic` carries only a `NodeInstanceId`, so every connection, port, and parameter diagnostic the validator reports is attributed to the owning node. The text names the port, but the projection cannot read it back without parsing a message. `ValidationProjection` therefore answers per node and per document only, and a port or connection view model can only inherit its owner's severity — a node with an out-of-range kernel size would mark its image connectors as invalid too. The detailed design asks `PortViewModel` to expose its own validation state (section 6.2).
- **Decision or next step:** Add an optional port or connection identifier to the diagnostic contract and populate it where the validator already knows it, then let the projection index the same diagnostics by port and by connection. Land it together with the port view model that consumes it, so the contract is designed against a real presentation need rather than ahead of one.
- **Evidence:** `src/VisionWeave.Contracts/Diagnostics/NodeDiagnostic.cs`, `src/VisionWeave.Application/Validation/WorkflowValidator.cs` (`ValidatePorts`), `src/VisionWeave.Application/Validation/ValidationProjection.cs`, `docs/design/visionweave-detailed-design.md` (section 6.2).
- **Owner:** VisionWeave maintainers.
- **Review again:** Before the inspector or the canvas renders port and connection validation state.
