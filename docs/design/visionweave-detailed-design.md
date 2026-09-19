# VisionWeave Detailed Design

## Status

- **Status:** Proposed; the repository skeleton is approved, and the foundation
  slice is implemented locally on `feat/foundation`: the ADR-0001 dependency
  boundaries, the port, document, ownership, and editor decisions of ADR-0003
  to ADR-0006, and a runnable path from a workflow document to native frames.
  The maintainer accepted those ADRs on 2026-09-19 after the review remediation
  of issues #1 and #2. The editor, `.vwflow` persistence, and the remaining node
  families are not implemented.
- **Owner:** VisionWeave maintainers.
- **Scope:** New WPF desktop application. This document does not prescribe an
  in-place migration of the legacy Aries solution.
- **Standards:** `dev-standards` revision
  `7528c0608e3bbcd437de366a00ea9c326839461f`, reviewed 2026-09-18.

### Aries reference boundary

Aries is a source of product scenarios and historical failure evidence only.
VisionWeave does not promise source, node, execution, serialization, layout,
or side-effect compatibility with Aries. Execution and persistence contracts
are designed from first principles for VisionWeave. Any future `.ar` import is
an optional boundary tool and must not constrain the core model.

## 1. Problem and goals

Aries proves the value of assembling OpenCV operations as a visual graph, but
its .NET Framework 4.7.2 / GraphX / MVVM Light architecture combines the graph
UI, OpenCV state, execution control, and persistence too tightly. This makes
interaction changes and safe algorithm expansion expensive.

VisionWeave is a new Windows desktop workbench for building, running, and
saving image-processing pipelines. It must provide a modern Fluent WPF UI, a
responsive node editor, typed OpenCvSharp processing nodes, deterministic
workflow execution, and a clean route for future plugin packages.

### Goals

1. Let a user compose an image pipeline visually without writing code.
2. Prevent invalid connections before execution using port type rules.
3. Run a directed acyclic graph (DAG) predictably, with cancellation,
   diagnostics, timing, and preview outputs.
4. Keep OpenCV native-memory lifetime explicit and safe.
5. Persist workflows in a stable, versioned, human-readable format.
6. Make it cheap to add built-in or third-party nodes without changing the
   canvas implementation.

### Non-goals for the first release

- Real-time camera acquisition, hardware trigger control, CUDA, and distributed
  execution.
- General cycles, loops, and feedback edges in graphs.
- Running arbitrary user C# / Python scripts inside a workflow.
- Sandboxed or out-of-process third-party plugins.
- Full parity with every Aries block, OCR, calibration, or legacy GraphX UI.

## 2. Constraints and reference patterns

| Constraint | Design response |
| --- | --- |
| Windows WPF desktop product | Target `net10.0-windows`; use WPF UI for the application shell and common controls. |
| High-quality node interaction | Use Nodify for selection, movement, panning, zooming, connectors, connections, and minimap. |
| Native OpenCV resources | The execution runtime owns output leases and disposes native resources when their last consumer finishes. |
| Existing Aries algorithms | Re-express validated algorithm behavior behind new node contracts; do not reference GraphX, MVVM Light, or old model types. |
| Long-term extensibility | Nodes register through a provider contract; the initial built-ins use the same contract as future plugins. |
| Standards adoption | Central package versions, nullable, folder-aligned namespaces, analyzers, tests, documented decisions, and CI gates. |

Orange is used as a product-design reference: widgets are independently
described components joined by directed data flow. Nodify is used as an editor
control, not as the application domain model. WPF UI is used for the shell,
navigation, property editors, dialogs, theming, and notifications.

## 3. Solution structure and allowed dependencies

```text
src/
  VisionWeave.Domain/          graph contracts and immutable value concepts
  VisionWeave.Contracts/       stable node, port, execution, and value contracts
  VisionWeave.Application/     use cases, validation, scheduler, undo/redo
  VisionWeave.OpenCv/          OpenCvSharp node definitions and executors
  VisionWeave.Persistence/     .vwflow serialization and schema migrations
  VisionWeave.PluginSdk/       public extension contracts
  VisionWeave.App/             WPF UI, WPF UI, Nodify, composition root
tests/
  VisionWeave.Domain.Tests/
  VisionWeave.Application.Tests/
  VisionWeave.OpenCv.Tests/
  VisionWeave.ArchitectureTests/
  VisionWeave.IntegrationTests/
```

| Assembly | May reference | Must not reference |
| --- | --- | --- |
| `Contracts` | BCL only | WPF, Nodify, OpenCvSharp, DI, persistence, Application |
| `Domain` | `Contracts` | WPF, Nodify, OpenCvSharp, persistence, Application |
| `Application` | `Domain`, `Contracts` | WPF, Nodify, OpenCvSharp |
| `OpenCv` | `Contracts` | WPF, Nodify, Application, Persistence |
| `Persistence` | `Domain`, `Contracts` | WPF, Nodify, Application, OpenCv |
| `PluginSdk` | `Contracts` | Domain internals, Application, WPF, Nodify, OpenCv |
| `App` | all needed composition-facing projects | none; it is the composition root |

`Contracts` owns the public, stable plugin surface: node/port and parameter
schemas, execution requests/results, value-lease abstractions, diagnostics,
and definition-provider registration. `Domain` owns workflow structure and
invariants. `OpenCv` and future plugins implement Contracts; `Application`
schedules them. The catalog is composed in `App`, which prevents plugins from
depending on Application or forcing OpenCvSharp into Domain.

Specifically, `NodeTypeId`, `NodeDefinition`, `PortDefinition`, parameter
schemas, `INodeDefinitionProvider`, `INodeExecutor`, execution requests/results,
`NodeDiagnostic`, and `DiagnosticCodes` are all declared in `Contracts`.
`Domain` declares only `NodeInstance`, `WorkflowConnection`, `WorkflowDocument`,
and the referential integrity and revision accounting of those collections.
Connection legality — direction, type, multiplicity, and cycles — is decided by
the Application validator, so a loaded document can be non-executable, displayed,
and repaired. `Domain` has no WPF, Nodify, OpenCvSharp, file-system, or
dependency-injection reference. `Application` has no WPF or Nodify reference.
Architecture tests assert every row of the table.

Each project explicitly sets its `RootNamespace`. Every source file uses the
root namespace plus all directory segments relative to its `.csproj` file. For
example, `VisionWeave.Application/Execution/WorkflowRunner.cs` declares
`namespace VisionWeave.Application.Execution;`.

## 4. Domain model

### 4.1 Node definition versus node instance

`NodeDefinition` is immutable, registered at startup, and describes a node
type. `NodeInstance` is persisted in a workflow and contains user-controlled
state only. Separating them makes saved documents independent of UI controls
and allows the catalog to evolve deliberately.

| Type | Responsibility |
| --- | --- |
| `NodeTypeId` | Stable provider-qualified identifier, e.g. `visionweave.opencv.gaussian-blur`. |
| `NodeDefinition` | Display metadata, category, port schema, parameter schema, executor type ID. |
| `NodeInstance` | Instance ID, type ID, parameter JSON, enabled state, canvas-neutral annotations. |
| `PortDefinition` | ID, direction, data type, multiplicity, optionality, and display name. |
| `WorkflowConnection` | Source instance/port and destination instance/port. |
| `WorkflowDocument` | Node instances, connections, revisions, metadata; guarantees referential integrity only, so a non-executable document stays loadable ([ADR-0004](../adr/0004-workflow-document-and-format.md)). |
| `NodeDiagnostic` | Stable code, severity, safe user message, node ID, and optional exception details for logs. |

`INodeDefinitionProvider`, `INodeExecutor`, `NodeExecutionRequest`, and
`NodeExecutionResult` live in `Contracts`. The App composition root resolves a
definition's executor type ID to an executor factory; the definition itself
does not import DI types. No algorithm executor receives a WPF `ImageSource`,
a Nodify control, or a ViewModel. Parameter values are deserialized and
validated at the definition boundary before execution.

### 4.2 Data types and connection rules

The initial catalog supports the following typed ports:

| Port value | Meaning | Typical producer / consumer |
| --- | --- | --- |
| `ImageFrame` | Read-only leased image result backed by `Mat`. | Image Source -> Blur |
| `Contours` | Immutable contour collection. | Find Contours -> Bounding Rect |
| `Rect` / `RectCollection` | Geometric output. | Bounding Rect -> Draw Rectangles |
| `Scalar`, `Number`, `Boolean`, `Text` | Scalar controls or measurements. | Contour Area -> comparison/output |
| `PointCollection` | Geometry for draw and calibration nodes. | Fetch Points -> Draw Points |

A connection is accepted only when directions are output-to-input, the target
accepts the source type, multiplicity allows it, both nodes exist, and adding
the edge does not introduce a cycle. Type conversions must be represented as
explicit nodes; there are no invisible runtime conversions.

### 4.3 Stable diagnostics

`VisionWeave.Contracts.Diagnostics.DiagnosticCodes` declares stable identifiers,
including `VW-GRAPH-001` (cycle detected), `VW-PORT-001` (incompatible port),
`VW-NODE-001` (missing node definition), and `VW-EXEC-001` (node execution
failed). UI messages, logs, and tests reference these identifiers rather than
duplicating strings.

## 5. Execution design

### 5.1 Execution contract

Each executor has a cancellation-aware asynchronous contract conceptually
equivalent to:

```csharp
Task<NodeExecutionResult> ExecuteAsync(
    NodeExecutionRequest request,
    CancellationToken cancellationToken);
```

`NodeExecutionRequest` contains validated parameters, typed input values,
workflow operation ID, and non-secret execution options. It never contains UI
objects. Expected invalid workflow conditions return validation diagnostics;
unexpected failures are captured at the runner boundary, logged once with the
original exception, and returned as a node failure diagnostic.

### 5.2 Scheduler

1. Capture an immutable `WorkflowSnapshot` with its monotonically increasing
   graph revision, resolved node definition versions, parameters, connections,
   and resource fingerprints.
2. Validate the snapshot and resolve each node definition.
3. Mark directly changed nodes and their downstream dependents dirty.
4. Build the dirty subgraph and topologically sort it.
5. Start ready nodes subject to configurable `MaxDegreeOfParallelism`.
6. Pass a node's typed outputs to dependent nodes only after successful
   completion.
7. On cancellation, stop scheduling new work, propagate the cancellation token,
   await started work, and release owned outputs.
8. If a node fails, mark only its dependent branch blocked; independent branches
   may finish.

The runner returns a complete run summary: operation ID, duration, node states,
diagnostics, and safe performance metrics. A global operation ID is included in
structured logs and node results. Editing a graph increments its revision. A
completed run only publishes previews and cached results when its snapshot
revision is still active; otherwise its result is retained only long enough for
safe cleanup. The default editing policy is to cancel the active run on a
semantic edit (parameters, nodes, or connections), while pure layout edits do
not cancel it.

Cache keys include node type ID and version, canonical validated parameters,
all input value identities, and each declared resource fingerprint. Nodes with
external side effects opt out of caching. Cache entries own independent leases
and are evicted by explicit budget policy, never by the canvas.

### 5.3 Native resource ownership

OpenCV `Mat` objects are not copied implicitly through the graph. A normal port
never exposes a raw `Mat`; it exposes an `ImageFrameLease` with a read-only
accessor. The runtime creates one consumer reservation per scheduled downstream
input before a producer output is published. A consumer must release its input
lease in `finally`; cancellation, validation failure, blocked branches, and
executor exceptions release any outstanding reservations through the same
cleanup path. The producer lease is disposed only when its reservations, cache
ownership, and preview-conversion fence all complete.

Rules:

- An executor treats input images as read-only and must not dispose them.
- An executor that needs mutation calls `CloneWritable()` and owns that clone;
  the input accessor never exposes a mutable shared `Mat`.
- An executor transfers ownership only of outputs it created.
- A preview renderer receives a downscaled managed bitmap; it never holds an
  execution `Mat` after conversion. Preview conversion registers a completion
  fence before the source lease can be released.
- Cached outputs have an explicit memory budget and eviction policy.

The implementation must include stress tests for fan-out, cancellation during
preview conversion, cache eviction, and a throwing plugin executor. Each test
asserts that native image leases return to zero after completion.

This avoids use-after-dispose errors while preventing canvas previews from
retaining full-size native images indefinitely.

## 6. WPF node editor rendering

### 6.1 Responsibility boundary

Nodify renders and interacts with a projection of `WorkflowNodeViewModel` and
`WorkflowConnectionViewModel` items. It does not decide whether a connection
is legal, run an algorithm, or own image data. The UI follows a one-way commit
protocol: **UI intent -> application mutation -> accepted graph delta -> UI
projection refresh**. ViewModel property setters never mutate Domain directly.

```text
NodeDefinition + NodeInstance
            ↓ mapping
WorkflowNodeViewModel ── ItemsSource ──> NodifyEditor
            ↓ DataTemplate
Nodify Node + Connector controls + WPF UI parameter editors
```

### 6.2 View models

`WorkflowNodeViewModel` exposes a node title, category, Nodify canvas position,
selected state, status, input and output port ViewModels, parameter ViewModel,
and optional preview bitmap. It has no OpenCV `Mat` property.

`PortViewModel` exposes display text, declared data type, connector anchor,
connection state, direction, and validation state. `WorkflowConnectionViewModel`
binds source/target anchors and owns only visual connection state.

The view model maps property-change actions into application commands:

| UI action | Application command | Result |
| --- | --- | --- |
| Drag node | `MoveNode` | Update persisted canvas position; no execution invalidation. |
| Create wire | `ConnectPorts` | Validate types, multiplicity, and cycle; update graph or show diagnostic. |
| Delete wire | `DisconnectPorts` | Remove connection and invalidate downstream output. |
| Edit parameter | `SetNodeParameter` | Validate, persist dirty state, invalidate downstream graph. |
| Run node / graph | `RunWorkflow` | Start a cancellable execution operation. |

Nodify's pending connection is visual-only until `ConnectPorts` accepts it.
Rejected connections are removed through the interaction completion callback
and surfaced as a port diagnostic. Node drag captures a start position, maps
canvas coordinates using the current editor transform, applies optional grid
quantization, and submits one move command when the drag ends. Multi-move,
paste, and delete are each a single atomic application command and one undo
unit. When Application emits a graph delta, the projection updates node and
connection collections in one dispatcher transaction; transient editor visuals
are then reconciled from the authoritative projection.

### 6.3 Templates

There are three reusable WPF templates, selected by node presentation metadata:

1. **Processing node:** title bar, input connectors on the left, parameter
   editor in the body, output connectors on the right, and status badge.
2. **Preview node:** processing-node layout plus a bounded thumbnail and an
   action to open the inspector.
3. **Group/comment node:** Nodify grouping container without an executor.

The `ParameterEditorTemplateSelector` chooses an editor based on the parameter
schema: WPF UI `NumberBox` for numeric ranges, `ComboBox` for enums,
`ToggleSwitch` for booleans, path-picker control for safe local files, and
specialized editors for geometry. Algorithms may contribute a parameter editor
template only in `App`; the domain parameter schema remains UI-neutral.

```text
┌──────────────────────────────────┐
│ ◈ Gaussian Blur            ✓ 8 ms │
├───────┬──────────────────────┬────┤
│ Image ○  Kernel size [ 5 ]   ○ Image
│         Sigma X     [ 1.2 ]       │
├───────┴──────────────────────┴────┤
│ preview: 320 × 180, click to open │
└──────────────────────────────────┘
```

The preview panel outside the canvas is the default place for full-resolution
inspection. The node thumbnail has a maximum configured pixel area so pan and
zoom remain responsive with hundreds of nodes.

### 6.4 Interaction and UX

- Left pane: searchable, categorized node library; drag or right-click search
  adds a node at the canvas location.
- Center: `NodifyEditor` with pan, zoom, box selection, multi-move, copy,
  paste, delete, minimap, and undo/redo.
- Right pane: property inspector for selected nodes, including validation help.
- Bottom pane: run history, node diagnostics, and structured log summary.
- Theme: WPF UI light/dark themes; type colors and error colors must meet
  contrast requirements in both themes.

## 7. Persistence and compatibility

The `.vwflow` format is a versioned JSON document. It stores only portable
workflow state, not WPF templates, native handles, local cache entries, or
absolute user-machine assumptions.

```json
{
  "schemaVersion": 1,
  "documentId": "...",
  "nodes": [
    {
      "id": "...",
      "typeId": "visionweave.opencv.gaussian-blur",
      "parameters": { "kernelSize": 5, "sigmaX": 1.2 },
      "portSchemaSnapshot": [],
      "extensionData": {},
      "layout": { "x": 480, "y": 120 }
    }
  ],
  "connections": [
    {
      "sourceNodeId": "...",
      "sourcePortId": "image",
      "targetNodeId": "...",
      "targetPortId": "image"
    }
  ],
  "resources": []
}
```

Load occurs in two phases. First, the reader validates JSON and schema
integrity while preserving unknown fields, node `extensionData`, and each
node's `portSchemaSnapshot` (port ID, direction, type, multiplicity, and
display metadata). Second, catalog-based executable validation resolves known
node types, validates current parameters and ports, and checks graph rules. A
missing plugin node becomes a non-executable placeholder rendered from its
snapshot; its raw JSON and connections remain intact so it can be restored when
the plugin returns. A document newer than the supported schema opens read-only
without destructive rewrite.

Migrations are explicit `IWorkflowMigration` implementations keyed by source
schema version. They are forward-only, idempotent, preserve unknown extension
data, and have round-trip/migration tests. The loader never silently drops an
unknown field merely because the current catalog lacks its definition.

Autosave writes to a separate recoverable working copy. Atomic save uses a
temporary file in the destination directory followed by replacement; failures
do not overwrite the previously saved document.

## 8. Node catalog and Aries migration

Initial node categories are Input/Output, Transform, Filter, Threshold,
Morphology, Contours, Draw, and Inspect.

| First-release node | Legacy conceptual source | Notes |
| --- | --- | --- |
| Image Source / Save Image | `MatSource`, export blocks | Local file paths only in MVP. |
| Resize / CvtColor / Crop | Transform blocks | Explicit formats and validation. |
| Blur / Gaussian / Median | Blur blocks | Parameter range validation. |
| Threshold / Adaptive / Canny | Threshold and filter blocks | Typed image output. |
| Erode / Dilate / MorphologyEx | Morphology blocks | Kernel schema shared through parameters. |
| Find Contours / Area / Bounding Rect | Contour blocks | `Contours` is its own typed value. |
| Draw Contours / Draw Rectangles | Draw blocks | Returns new image; never mutates input. |

Every migrated node receives output-oriented regression tests against curated
sample images. Migration moves algorithm intent and behavior, not the old
GraphX controls, Aries view models, or serialization format.

## 9. Configuration, diagnostics, and security

`appsettings.json` contains only safe defaults. Typed Options classes own
execution concurrency, preview limits, cache limits, autosave interval, and
plugin search locations. Startup validates values and reports actionable,
non-secret errors.

Structured logs include `OperationId`, `WorkflowId`, `NodeId`, `NodeTypeId`,
`DiagnosticCode`, and elapsed milliseconds. Logs never include full image
payloads, credentials, tokens, or unsafe path contents. Node failures are
logged once at the execution boundary with the original exception as context;
the UI receives a safe diagnostic.

Plugins are disabled by default in the first release. `PluginSdk` is still
implemented as a small stable contract so built-in providers exercise the same
registration path. External loading is not exposed until its dedicated ADR and
integration tests are accepted.

The future plugin manifest must declare plugin ID, version, node type IDs,
SDK minimum/maximum version, target framework, source/license metadata,
assembly hashes or signature policy, and dependency resolution directory.
Duplicate node type IDs fail discovery deterministically. The loader uses a
collectible `AssemblyLoadContext` where feasible, reports load and execution
failures with stable diagnostics, and allows a failed plugin to be disabled
without preventing the application from starting. In-process plugins remain
trusted code; sandboxing is a future feature, not an implied security boundary.

## 10. Testing and verification

| Tier | Proof |
| --- | --- |
| Domain unit tests | Port compatibility, multiplicity, cycle detection, stable diagnostics, graph changes. |
| Application unit tests | Topological order, dirty-subgraph selection, cancellation, branch blocking, undo/redo. |
| OpenCV integration tests | Expected pixels / geometry for each migrated node, disposal and cache behavior. |
| Persistence integration tests | Save/load round trip, malformed document rejection, migrations, missing-node placeholders. |
| Architecture tests | Dependency direction and no WPF/OpenCV reference in Domain. |
| UI smoke tests | Canvas add/connect/delete, property edit, run/cancel, light/dark template rendering. |

Tests use xUnit and Shouldly. Test names use the form
`Member_condition_expected_result`, e.g.
`ConnectPorts_incompatible_image_and_contours_returns_port_diagnostic`.

The initial CI command sequence is restore, build, test, format verification,
and analyzer verification. `global.json`, `.editorconfig`, analyzer choices,
and suppression records are all versioned. NuGet audit remains enabled;
dependency versions are centralized in `Directory.Packages.props`.

## 11. Delivery plan

1. Create the solution skeleton, standards reference, package and analyzer
   configuration, documentation layout, ledger, and CI quality gates.
2. Record ADRs for dependency direction, Nodify selection, `.vwflow` schema,
   and `Mat` ownership.
3. Implement Domain, its unit tests, and architecture-test baseline.
4. Implement Application graph edits, validation, undo/redo, and scheduler.
5. Build the WPF UI shell and Nodify canvas backed by generic node templates.
6. Add the run/status/preview path and native resource ownership tests.
7. Migrate first-release OpenCV nodes and golden-workflow integration tests.
8. Add persistence, recovery, final UI smoke tests, and packaging.

## 12. Decisions requiring review

1. Confirm `net10.0-windows` as the initial target framework and Windows-only
   product boundary.
2. Confirm the DAG-only MVP; loops and streaming will require separate,
   explicit execution semantics.
3. Confirm that all image processing nodes return a new output image instead of
   mutating their input.
4. Confirm `.vwflow` JSON as the public workflow format.
5. Confirm in-process plugins are deferred until the core node contract is
   stable.

## 12.1 Required records before implementation

Implementation starts only after these records exist and link back here:

| Record | Decision captured | Status |
| --- | --- | --- |
| [ADR-0001](../adr/0001-directed-project-dependencies.md) | Contracts, Domain, Application, OpenCv, Persistence, PluginSdk, and App reference rules. | Accepted |
| [ADR-0002](../adr/0002-initial-framework-dependencies.md) | WPF UI, Nodify, OpenCvSharp, and test package selection; license, target framework, and audit posture. | Accepted |
| [ADR-0003](../adr/0003-port-value-types.md) | Port type identity, contract value representations, compatibility table, multiplicity, and the deferred batch/collection flow. | Accepted |
| [ADR-0004](../adr/0004-workflow-document-and-format.md) | `WorkflowDocument` versus executable snapshot, `.vwflow` schema, definition versions and migrations, unknown-node placeholders, resource references, and forward-version policy. | Accepted |
| [ADR-0005](../adr/0005-native-resource-ownership.md) | Lease state machine, reservations, executor resource scope, cache ownership, preview fence, cancellation quarantine, and lease-ledger tests. | Accepted |
| [ADR-0006](../adr/0006-editor-and-ui-commit-protocol.md) | Nodify and WPF UI scope, UI intent/rollback protocol, undo granularity, and the deferred automatic-layout and subgraph capabilities. | Accepted |
| [docs/ledger/standards-deviations.md](../ledger/standards-deviations.md) | Each approved exception to the adopted standards, or an explicit "none" baseline. | No deviations |

## 13. Alternatives considered

| Alternative | Decision | Reason |
| --- | --- | --- |
| Continue GraphX in a modern shell | Rejected | Retains legacy graph/UI coupling and does not provide the desired MVVM-oriented editor foundation. |
| Implement custom node canvas | Rejected | Recreates mature selection, connector, zoom, and interaction behavior before delivering product value. |
| Put OpenCV logic in WPF ViewModels | Rejected | Makes UI tests, plugins, and native resource ownership harder to reason about. |
| Serialize WPF/Nodify objects | Rejected | Couples documents to UI library internals and blocks safe format evolution. |
| Use cycles from day one | Rejected for MVP | A cycle needs iteration, feedback, termination, and lifetime semantics not present in a DAG scheduler. |

## 14. Review checklist

- [ ] Domain and dependency boundaries are sufficient for plugin and UI
      evolution.
- [ ] `Mat` ownership rules are implementable and testable.
- [ ] Node rendering supports both generic and specialized parameter editors.
- [ ] Connection validation is enforced in Application, not only the UI.
- [ ] `.vwflow` format preserves the workflow without persisting UI/runtime
      implementation objects.
- [ ] MVP scope is small enough to deliver while retaining a valid extension
      path.
- [ ] Any deviation from `dev-standards` is recorded before implementation.
