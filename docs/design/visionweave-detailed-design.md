# VisionWeave Detailed Design

## Status

- **Status:** Proposed; the repository skeleton is approved, and the foundation
  slice is implemented locally on `feat/foundation`: the ADR-0001 dependency
  boundaries, the port, document, ownership, and editor decisions of ADR-0003
  to ADR-0006, and a runnable path from a workflow document to native frames.
  The maintainer accepted those ADRs on 2026-09-19 after the review remediation
  of issues #1 and #2. On top of that foundation the shell theme, the projected
  Nodify canvas, `.vwflow` read/write, and the inspector and document commands of
  sections 6.5 to 6.7 are implemented, together with the run, cancel, and preview
  path of 6.8, the run marks of 6.9, and the tags the catalogue is filtered by; the
  remaining node families and the minimap, copy and paste, and automatic layout are
  not.
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
  VisionWeave.App/             WPF shell, canvas projection, view models, theme, composition root
tests/
  VisionWeave.Domain.Tests/
  VisionWeave.Application.Tests/
  VisionWeave.Persistence.Tests/
  VisionWeave.App.Tests/
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

Parameter values are validated where a node's definition resolves, not by the
executor that happens to read them: `VW-PARAM-001` reports a value whose shape
does not match the declared kind, `VW-PARAM-002` a numeric value outside the
declared bounds, `VW-PARAM-003` an option the definition does not declare,
`VW-PARAM-004` a name the definition does not declare, and `VW-PARAM-005` a
required parameter the document leaves unset while the definition declares no
default. A document that reports any of them never becomes a snapshot, so an
unreadable value cannot reach an executor as a runtime failure. A disabled node
is never executed, so only its value shapes are checked, not whether a required
value is present.

Editing reports its own outcome in the same way rather than throwing into a
binding layer: `VW-EDIT-001` is a refused edit, which leaves the document
untouched, and `VW-EDIT-002` is an undo or redo with nothing to reverse.

A validation run is handed to the editor as a `ValidationProjection`: an
immutable view of one `ValidationResult`, indexed by stable node instance
identifier and tagged with the revision it was computed from. Selection is
presentation state that changes far more often than the document and can
outlive the edit that produced the projection, so the projection answers
`DiagnosticsFor`, `SeverityOf`, and `Select` from its own snapshot instead of
re-validating, and it reports nothing for an identifier the document no longer
contains. `Select` returns the selected nodes' diagnostics together with the
document-level ones, in the order validation reported them, and does not depend
on the order or the multiplicity of the identifiers it was given. `Matches`
tells the caller whether the projection still describes the current revision; a
layout-only move changes no revision, so a node move does not invalidate it,
while a semantic edit does, and a superseded projection is replaced rather than
queried. Port-level and connection-level attribution is available through the
diagnostic target: a diagnostic that is narrower than its node names the port, the
parameter, or the connection it describes with identifiers the document already
carries, so the projection answers `DiagnosticsForPort`, `DiagnosticsForParameter`,
and `DiagnosticsForConnection` without parsing a message. A port or a parameter
reports its own conditions preceded by the conditions its node reports about
itself, because a node-level failure is what makes its members unknowable, while a
connection belongs to two nodes and inherits neither. A condition the projection
cannot attribute more narrowly stays visible at the node or document scope it
names, and a query for an element it never saw answers with the nearest broader
scope instead of a badge that describes something the document no longer holds.

The host reports its own conditions the same way: a setting outside the range the
runtime can honor is `VW-CONFIG-001`, and the application refuses to start rather
than running with a substituted value.

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

A node reports success together with its output values. Before the runtime hands
those values to the scheduler it checks them against the node definition's output
ports: the port must be declared as an output, the value must carry the port's
declared type, and an image frame lease must be one the node created rather than
one it received as an input, used for exactly one output port. A result that
breaks that contract fails the node with `VW-EXEC-010` to `VW-EXEC-013` and
releases the frames it reported.

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

**The run's clock is the one it is given.** A run measures the cancellation grace
period and the durations it reports on the `TimeProvider` the host supplies, not
on the machine clock, so one clock decides both when the runtime stops waiting
for a node that is ignoring cancellation and what the run reports having taken.
The host hands the runtime the same clock it hands the editing session, so the
waits, the reported durations, and the coalescing window of a parameter edit all
count the same time. A test reaches a grace expiry, and the moment a completed
level gives up its pending wait, by moving that clock rather than by sleeping
through it.

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

### 5.4 File-backed runs as built

The first two file-backed nodes are the pair that makes a stored workflow runnable.
`ImageSourceExecutor` reads the file its path names as a color frame and publishes
the read `Mat` as a lease it created and no longer owns, so the runtime releases it
once its consumers are done and a failed read disposes what it allocated.
`SaveImageExecutor` is a sink: it declares no output port, writes the incoming frame
under a temporary name in the destination folder and moves it onto the destination,
so a failed or cancelled write never leaves a half-written image where a readable one
was, and it refuses an existing destination until the document's `overwrite` is set
(ADR-0012, decisions 6 and 8).

The working directory of a run is the folder that holds the document being run, and it
travels with the run: `NodeExecutionEnvironment` in `Contracts` is carried by the
`ExecutionPlan` and handed to every executor in the request the node receives, so no
executor reads a process-wide directory and two runs of two documents in one process
resolve the same relative path differently on purpose. `WorkDirectoryPath.TryResolve`
turns a declared path and that directory into an absolute path or a refusal, and every
unusable input is a refusal rather than an exception: a rooted path, a blank path, a
path that escapes the folder, a path that names a directory, an illegal character, and
an over-long path all fail the node that declared them with the diagnostic vocabulary
the run already has, which blocks the branch downstream of it.

A run the user stops is an outcome rather than a fault. The runner records the levels
it did not execute as cancelled, returns its summary, and releases every frame it held
on the way out, so the shell can report the stop in the readout while keeping what the
run had already produced — the nodes that completed are still reported, and the lease
ledger returns to zero.

## 6. WPF node editor rendering

### 6.1 Responsibility boundary

Nodify renders and interacts with a projection of `WorkflowNodeViewModel` and
`WorkflowConnectionViewModel` items. It does not decide whether a connection
is legal, run an algorithm, or own image data. The UI follows a one-way commit
protocol: **UI intent -> application mutation -> accepted graph delta -> UI
projection refresh**. ViewModel property setters never mutate Domain directly.

An intent reaches the document as an `IDocumentCommand` handed to
`DocumentCommandHistory.Execute`, which owns the single undo stack. A command
carries the data its own reversal needs — the identifiers it created, the values
it replaced, the entries it removed — so undo never rebuilds a whole document and
the UI never supplies the state to restore. The returned `DocumentCommandResult`
says whether the document changed and, when it did not, why; a refused edit is
reported with diagnostics and leaves the document and the history untouched.
Connection legality is judged by `WorkflowValidator.ValidateConnection`, which
reports only the diagnostics a candidate wire itself would introduce, so the
canvas can judge a pending connection while the rest of the graph is still
incomplete.

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
binds source/target anchors and owns only visual connection state. A port reads its
own validation state from `ValidationProjection.SeverityOfPort`, which reports the
conditions of that port together with the conditions its node reports about itself,
so a port is not marked by a failure of a parameter the node declares. A connection
reads `SeverityOfConnection` instead, so a wire the validator rejected is marked
itself rather than through either endpoint.

The view model maps property-change actions into application commands:

| UI action | Application command | Result |
| --- | --- | --- |
| Drag node | `MoveNode` | Update persisted canvas position; no execution invalidation. |
| Create wire | `ConnectPorts` | Validate types, multiplicity, and cycle; update graph or show diagnostic. |
| Delete wire | `DisconnectPorts` | Remove connection and invalidate downstream output. |
| Edit parameter | `SetNodeParameter` | Validate, persist dirty state, invalidate downstream graph; one parameter is one undo unit, and a refused value changes nothing (see 6.7). |
| Run node / graph | `RunWorkflow` | Start a cancellable execution operation. |

Nodify's pending connection is visual-only until `ConnectPorts` accepts it.
Rejected connections are removed through the interaction completion callback
and surfaced as a port diagnostic. Node drag captures a start position, maps
canvas coordinates using the current editor transform, applies optional grid
quantization, and submits one move command when the drag ends. (As built, no
transform math is needed: `ItemContainer.Location` is already graph space, so the
rounded location a completed drag leaves is what is submitted — see 6.6.)
Multi-move,
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
- Theme: the dark-amber semantic theme of section 6.5, which the framework's own
  resource keys are aliased onto. A light or high-contrast theme replaces the
  token values without changing a control template, and every theme must keep the
  contrast ratios the visual direction requires of text and of keyboard focus.

### 6.5 Shell foundation and semantic theme

The first shell ships the six regions the visual direction documents and nothing
editable inside them: a top strip holding the application identity, the document
commands, and the run state; a node catalogue that names what exists; the surface
a document will be edited on; an inspector for the current selection; a status
area for durable state; and the managed preview under the canvas, which draws the
newest image a run published (6.8). Each region is named in the markup —
`TopBarRegion`, `NodeCatalogueRegion`, `CanvasRegion`, `PreviewRegion`,
`InspectorRegion`, `StatusRegion` — so a test can hold the layout to the
documented architecture, and the canvas region presents the editor the projection
package put there (6.6).

The shell carries no business rule in code-behind: the window initializes its
markup, hands its view model to the data context, and attaches the region a
snackbar is drawn in. Everything else the user sees is derived from the editing
session by `MainWindowViewModel` and `ShellStatus`, and a test refuses any further
member in the code-behind.

Three rules hold the visual system together.

**A colour is named once.** `Themes/Tokens.xaml` is the only file in the shell
that carries a colour literal. Each token declares a `Color.*` value, a `Brush.*`
alias that reads it, and — for the values the framework theme paints with — an
alias onto the WPF UI resource key it replaces (`ApplicationBackgroundBrush`,
`TextFillColorPrimaryBrush`, `AccentFillColorDefaultBrush`, and the rest), so
framework controls join the palette without a control template of ours changing.
`Themes/Shell.xaml` holds the shell's styles and names tokens only. Tests assert
the documented values, that every token has a brush that reads it, that the
aliases follow the tokens, that no other file names a colour, and the contrast
ratios the direction requires.

**Status is never colour alone.** The status area names a severity in words beside
the colour it paints with, and an announcement carries the same word. Colour is
the second signal, not the only one.

**A key is declared before the template that reaches for it.** A `StaticResource`
is resolved against what the dictionary has already declared, so a template that
reaches for a key declared below it fails when the template is applied — while the
shell draws its first node, not at build time. A test walks the shell dictionary in
document order and refuses a reference to a key that is not yet declared.

The status area reports what lasts: the document state (never saved, unsaved,
saved), the operation the shell is running or the state it returned to, the last
run outcome, and the newest condition with its stable code. The run outcome is a
reading of its own rather than a second use of the background-operation field: it
names the run as running, completed, failed, stopped at the user's request, or not
run together with the reason, and it survives the operation that wrote it, because
the shell reports the operation's completion afterwards. The snackbar reports
what happens once: the `IUserNotificationPresenter` seam of ADR-0008 is now served
by `SnackbarNotificationPresenter`, which shows the safe message and the stable
code and never the diagnostic's exception. A cancelled operation is an outcome
rather than a failure: it reports "Stopped at your request", records no condition,
and clears the one reported before it. Startup keeps its modal message box as the
single documented exception.

Running is two gestures, not one mode: the header offers Run and, beside it,
Cancel, which is enabled exactly while a run executes and answers through the
runtime's own cancellation. `Ctrl+R` runs the document the way `Ctrl+O` opens one,
and a run that was refused — because the document was never saved, or because
validation reports an error — says why where the outcomes are read instead of
starting and failing a node per path (7).

Keyboard focus uses the accent colour, which the theme test holds to the non-text
contrast ratio against every surface the ring can be drawn on. `Ctrl+O` is bound
to the same command as the shell's Open action, and `Ctrl+R` to the same command
as its Run action.

The Open flow reaches the session through two seams rather than through dialog
calls inside the view model: `IWorkflowFileChooser` asks for the file, with a
Windows implementation behind it, and `OpenDocumentCommand` opens it through the
command boundary. A dismissed dialog, a failed open, and a cancelled open are
therefore covered by tests that never open a window.

WPF binds only to public members, and a binding to anything else fails silently —
an empty field rather than an error. The view models are internal types whose
bound members are public, and a test walks every binding path in the markup to
keep it that way. The XAML designer shows the shell with sample content built from
the real node catalog, the real validator, and real document commands
(`ShellDesignData`), so it cannot present a shell the application would never
produce.

### 6.6 Canvas projection as built

`CanvasProjection.Project` turns committed state into what the surface draws:
`CanvasProjectedDocument` holds the node presentations in drawing order and the
wire presentations. It reads the document, the catalog, the validation projection
and the selection, and writes nothing, so the same document always draws the same
way and a gesture that was refused simply leaves the answer it produced. The
canvas keeps no incremental state: the session reports a new document or a new
projection and the whole surface is rebuilt from it, so no node presentation
outlives the document that produced it and pan and zoom survive because they
belong to the editor rather than to the projection.

`WorkflowDocument.Nodes` is a dictionary, so the order it enumerates in is not
part of its contract. Nodes are drawn ordered by position — row, then column,
then instance identifier — which keeps the surface stable and puts a node nearer
the top-left behind one laid over it. A node shows its label when it has one, the
definition's display name when it does not, and `Unknown node type` when the
definition does not resolve; its caption names the type and version, with
`· not installed` appended in the last case. Ports come from the definition, or
from the schema a document remembered when the definition does not resolve, where
a port that carries no label is named by its identifier. A node whose ports are
unknown presents no connectors: a connector that accepted a wire the document
cannot describe would be a promise the shell cannot keep.

Each presentation carries the worst condition its element owns, taken from the
validation projection for that revision, and the condition's first diagnostic in
the same words the status area uses — severity named, stable code, safe message.
The severity also travels as a word, so a marked node, port, or wire reads as
marked without relying on its colour. A wire is drawn only when both of its ports
can be named; a wire to a port this build cannot name has nothing to attach to and
is left out, while the nodes it belonged to are still shown, and deleting one of
them removes the wire with it.

The connector contract is worth stating because it is invisible in the markup.
`Connector.Anchor` is a graph-space `Point`, not a reference to an element, and
the connector control is what publishes it: the port presentation binds it
`OneWayToSource`, and a wire binds `Source.Anchor` and `Target.Anchor`, so a wire
follows the ports it joins instead of a copy of where they were. Nodify stops
publishing the anchor once `IsConnected` is false, and its default is false, so
the connector style sets it true — a connector left at the default would keep the
point it started at and every wire would hang in the wrong place. The wire a drag
is drawing is a `PendingConnection`, visual only, drawn backwards when the drag
began at an input so the curve follows the pointer whichever end it started at.

Every gesture becomes an application command.

- **Add** places the catalog's latest version of the type at the middle of what
  the viewport shows, stepped aside in fixed increments until the spot is free, or
  at the origin before the editor has reported a viewport. A type this build does
  not hold is refused with `VW-NODE-001` and adds nothing.
- **Connect** turns the two ports a drag joined around when the drag began at an
  input, because the document's convention is that a wire leaves an output.
  Direction, type, multiplicity, and cycle rules stay the validator's, so a pair
  that cannot be joined is passed on and refused there.
- **Disconnect** removes the wire the gesture named, which is one edit and one
  undo unit.
- **Move** commits the positions a completed drag left, rounded to whole
  graph-space units so a document holds no sub-pixel noise from a pointer. A drag
  that ended where the node started differs nowhere and is not an edit, and a
  movement of nothing is not a history step.
- **Delete** removes the whole selection as one command and one undo unit —
  `RemoveNodesCommand`, which resolves every named instance before removing any of
  them — so an undo brings the nodes and the wires between them back together. A
  delete with an empty selection cannot execute, so it never becomes a step with
  nothing in it.
- **Undo and redo** are the session's, and the canvas only reports whether they
  are available.

A refused gesture needs no rollback of its own. The projection it was drawn from
is committed state, and a refused command leaves that state untouched, so the
surface is already what the user should see; the reason travels to the status area
as the validator's diagnostics rather than as a completion of its own. Selection
is one selection: it lives in the session, the canvas mirrors it onto the node
presentations and turns a container's flag back into the session's selection
behind a guard, so a selection the canvas applied is not read back as one the user
made. Edits made anywhere else in the shell redraw the surface, because the canvas
learns about the document and the selection from the session rather than from its
own gestures.

Parameter editors and the diagnostics panel arrive with 6.7. Per-node preview,
the minimap, copy and paste, automatic layout, and run-state presentation are not
part of this slice.

### 6.7 Inspector, document commands, and read-only presentation as built

The inspector presents the selected node: its title and caption, one field per
parameter the definition declares, and the conditions that selection owns. A field
is picked from the parameter's declared kind rather than from a template selector —
a switch for a boolean, a list of the declared options for an option parameter, and
a text field for everything else — so one value is edited one way and the surface
never shows two ways at once. Section 6.3 describes the per-kind template selector
that takes over once nodes contribute editors of their own, which is package 8's
work rather than this slice's.

A field holds three things: what the document stores for that parameter, the
severity the validation projection attributes to it, and the condition in the words
the status area uses. It commits through the inspector instead of the document: a
typed value is applied with Enter, which the field's own caption says, because
typing is not a whole gesture the way toggling a switch is; a switch and an option
commit the change itself, and an option that has chosen nothing is refused rather
than sent as a value.

Three outcomes stay distinguishable at a field. An accepted value becomes a
`SetNodeParameterCommand` through the session, so it is one edit, and the history
merges later edits of the same parameter into that unit while they are within
`ParameterCoalescingWindow` — one parameter is the unit a user thinks in, and a
keystroke is not. A merged unit keeps the value the user settled on, so redoing it
restores where the edit ended rather than where it began. A value the definition
refuses is reported as a condition and changes nothing: the field marks itself with
the diagnostic it earned, the document keeps the value it had, and the same
diagnostic travels to the status area. A commit that cannot be made at all — no node
selected, or a document that may not be edited — is not offered: the whole field
list is disabled and the inspector says why.

The diagnostics panel is the same projection read a second way. It lists the
conditions of the current selection, each with its severity as a word, its stable
code, and its safe message, so a marked node, port, parameter, or wire can be read
as text rather than only as a colour.

Document commands are the shell's, and the session remains the authority. New, open,
save, and save-as run through the command boundary of ADR-0008, so a running
operation is reported, a cancellation is a stop rather than a failure, and an
unexpected failure becomes one safe diagnostic and one log entry. Opening a file
asks first when the document holds changes its file does not — save, discard, or
cancel, with the file named in the question — and a working copy beside the file is
offered as a recovery when one exists. A recovered session starts with unsaved
changes, because its content is not yet the content of its file.

Reading a file and adopting what it produced are two steps on purpose, and they run
on different threads. Reading blocks, so it runs away from the shell and the window
keeps answering; the session then adopts the result on the thread the command was
started on, which is the thread the window's bindings belong to. That is not a
detail: adopting announces the new document, and a binding refuses a notification
raised on another thread — the notification walk stops there, so the window keeps the
projection of the document it replaced and never learns that the selection of that
document has to go. `EditorSession.Read` and `EditorSession.ReadWorkingCopy` are the
reading half, `EditorSession.Adopt` is the half that replaces the document and
announces it, and `EditorSession.Open` and `EditorSession.Recover` pair the two for a
caller with no window. Adopting a document also empties the selection, because a
selection names instances of the document it was made in.

A document this build does not understand — one whose schema version is newer — is
opened read-only: it is shown, with its nodes, ports, and stored parameters drawn
from what the file holds, and nothing about it may be edited, because writing it back
would discard content this build cannot see. The reason is drawn on both surfaces the
document would be edited on, the catalogue offers no addable type, the header's
delete stays disabled even with a node selected, the field list is disabled as one
list, and the rule belongs to the session rather than to the markup: a gesture that
reaches the canvas, or the session directly, changes nothing and reports
`VW-FILE-002`.

The catalogue is searchable by display name and by type identifier, because the
identifier is what a document, a diagnostic, and a stored parameter name a node by. A
search that matches nothing says so instead of showing an empty list.

The catalogue's second axis is the tag, and it is the axis the category cannot be: a
category says where a type sits in the library, and the categories are deliberately
coarse — seven types sit in Filter — so "the node that finds edges" is not a thing a
user can ask for by category. `NodeDefinition.Tags` carries the words a type is
filtered by, declared by the provider out of one vocabulary rather than written freely
per definition, because the filter row is built from the words the catalog actually
holds: one `filter` and one `filtering` would be two chips that each show half the
answer. `OpenCvNodeTags` is that vocabulary for the built-in nodes, and the catalog's
tests hold the two ends together — every definition carries at least one word out of
it, and every word in it is carried by some definition — so a chip can neither appear
that nothing answers nor be missing for a word the definitions use.

The filter row is built from the definitions rather than from the provider's list, so
the shell needs no reference to a provider and a plugin's own words are filterable
without this shell changing. Chips intersect rather than union: each one a user adds
asks for less, so `mask` and `threshold` together offer the two thresholded nodes and
not the six that carry either word. The row carries a first chip that clears the
filter, and the chip whose word is in force is painted in the accent, so which filter
is on reads without counting what is left. The selected word is not matched by the
search box: the search matches the name a user reads and the identifier a document
stores, and a search that answered with a word no entry showed would offer a type whose
reason for matching the user cannot see. The summary and the notice name whichever two
filters are in force, and a definition's own words are drawn beside the name it is
chosen under, because a tag the user cannot see is a tag they cannot learn.

Tags are catalog metadata and not document data: a type's words travel with the
definition that declares them, no `.vwflow` field records them, and a document written
before or after this change loads identically. They stay off the canvas as well, which
has a title line and a caption line and would only repeat what the picker answered.

### 6.8 Run, cancel, and the managed preview as built

`RunWorkflowCommand` is the shell's one path to a run, and everything it refuses is
refused before a node starts: a document that has never been saved has no folder for
its file paths to resolve against (ADR-0012) and is refused with `VW-FILE-004`, and a
document the capture reports validation errors for is refused with those errors,
because the inspector already shows the whole list at the node and the parameter each
condition belongs to. The command owns two gestures: `Command`, the toolkit's
asynchronous command whose own state disables a second run and whose cancellation is
what Cancel trips, and `CancelCommand`, which is enabled exactly while a run executes
and shares that state rather than tracking a flag of its own.

The run itself executes on the thread pool — a file read, a filter, and a file write
are blocking — and the boundary of ADR-0008 is the only path back to the shell, so a
fault the run did not anticipate is reported once rather than escaping into a
binding. The runtime's summary is translated into the run outcome described in 6.5,
and what it leaves behind is reported as conditions with one deliberate omission: the
cancellation of each node that never started is not repeated as a condition per node,
because the run outcome already names the gesture.

The preview is a seam rather than a call inside the command: `RunPreviewObserver`
serves `IExecutionOutputObserver`, converts the first image output of a node while
the runtime still owns the frame — the task it returns is the fence that keeps the
lease alive — and hands a frozen copy to an `IRunPreviewPresenter`. Converts run
where the run executes, so `IRunPreviewPresenter` decides where a preview may touch
the bindings; the shell's implementation marshals to the window's dispatcher and
waits, which keeps the run's completion ordered after the preview it produced.
`PreviewViewModel` holds that copy, the node that published it, and its size and
pixel layout, and it forgets all three when the document is replaced, because a
preview of a document that is no longer open describes nothing. The region's note
stands in for the image while nothing has run, so an empty frame never reads as a
preview that failed to draw. The composite — a real image read from the document's
folder, resized, written beside it, drawn in the region, and stopped while a node is
executing — is covered by the shell's own smoke test (10).

### 6.9 Run marks on the canvas as built

The summary the run produces is not only a readout. `ShellStatus` keeps the newest
one beside the outcome it derives from it, and the canvas redraws from it, so every
node the run covered says what the run did to it: `Succeeded` or `Failed` in a word
and in colour, or `Cancelled`, `Blocked`, or `Not run` when the run ended some other
way, with the time the runner measured for the nodes it measured one for
(ADR-0013). The node's tooltip carries the run's own condition for it, so the reason
a node failed is on the node rather than only in the status area. The mark is
presentation state of its own kind: it is drawn on the node, and the ports and wires
keep the validation severity they already showed, because a run condition describes
an execution rather than the wire a value arrived on.

A mark belongs to a revision. A run describes one execution of one revision of one
document, and `WorkflowRunSummary` carries both facts, so the projection marks nodes
only while the document on screen is the document that ran, at the revision it ran
at — the rule `ValidationProjection.Matches` already follows. A parameter edit, a
new wire, or another document clears the marks, because the graph on screen is no
longer the graph that ran; dragging a node keeps them, because a move is layout
rather than a change to the graph and moves no revision; and starting a run clears
them until it reports, because a run in flight has no outcome yet. Nothing about a
run is written into `.vwflow`: a saved document never claims that its nodes
succeeded.

A node that is still executing is deliberately not marked, and that is the one thing
this presentation does not show. `NodeRunState` is a terminal-state enumeration and
the runner has no progress seam, so marking a node as running would need a new
observer contract, an implementation that marshals to the window's thread at every
node boundary, and an answer for a node still executing inside a quarantined
executor. The status area says `Running…` for the run as a whole instead.

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
integrity while preserving unknown fields and node `extensionData`, and reads
each node's `portSchemaSnapshot` (port ID, direction, and, when the saving build
recorded them, type, multiplicity, optionality, and display metadata) into the
node model. Second, catalog-based executable validation resolves known
node types, validates current parameters and ports, and checks graph rules. A
missing plugin node becomes a non-executable placeholder rendered from its
snapshot; its raw JSON and connections remain intact so it can be restored when
the plugin returns. A document newer than the supported schema opens read-only
without destructive rewrite.

The implemented version 1 writer stores `schemaVersion`, `documentId`, `name`,
`createdUtc`, `modifiedUtc`, `revision`, the optional `appVersion`,
`requiredPlugins`, `resources`, `nodes`, `connections`, and every field the build
does not model. A node entry records `id`, `typeId`, `typeVersion`, `parameters`,
`portSchemaSnapshot`, `layout`, and `extensionData`, plus `label` and `enabled`
when they differ from their defaults; a connection records its own `id`. A file is
read when it declares `documentId`, `name`, `createdUtc`, and `revision`;
otherwise it reports `VW-FILE-001` and yields no document. A schema version this
build cannot migrate reports `VW-FILE-002` and opens read-only, and an entry or
field that cannot be represented is skipped with `VW-FILE-003` rather than failing
the load. Reading never changes the stored revision or modification instant,
because a load is not an edit.

A resource is a typed reference: a `file` kind with a path and an optional digest
the user supplied, or a kind this build does not model, kept verbatim so a later
build still finds it. Because a resource is user-editable data a run will read,
adding one counts as an edit — it changes the revision and makes the session
report the document dirty. A remembered port schema is a rendering fallback
rather than a second source of truth: refreshing it changes no revision, and a
member this build cannot read is treated as one that was never recorded. Both
contracts, and the read and write policy for an entry that cannot be represented,
are recorded in
[ADR-0011](../adr/0011-resource-references-and-port-schema-snapshots.md). The
machine fingerprint of a resource stays runtime-only, because a document stores a
reference and not a fingerprint, and no migration is required to read either
field: both are version 1 fields that the format already defines.

Migrations are explicit `IWorkflowMigration` implementations keyed by source
schema version. They are forward-only, idempotent, preserve unknown extension
data, and have round-trip/migration tests. The loader never silently drops an
unknown field merely because the current catalog lacks its definition.

Autosave writes to a separate recoverable working copy. Atomic save uses a
temporary file in the destination directory followed by replacement; failures
do not overwrite the previously saved document. The editor session reports an
autosave attempt as an outcome rather than a Boolean — nothing to write, a copy
written, a document that must not be written, or a write the file system refused
— and a refused or failed write arrives as `VW-FILE-002` or `VW-FILE-001` with
its exception kept for structured logging, so a scheduler or a timer never has to
interpret an exception to decide what to do next.

`WorkflowSession` is the editing session a caller works through, and it belongs
to `Persistence` because it is the only layer that sees both the document and the
file. It binds the document to its file and records whether that file may be
written, whether the document has unsaved changes, and which diagnostics the load
produced. Unsaved-change tracking counts every change, including a move, because
a moved node is saved state; the document revision alone would miss it.
`WorkflowSession.New`, `Open`, and `Recover` produce a session, `Save` writes it
atomically and then drops the working copy, `TryAutosave` writes the working copy
only for a dirty, writable document that has a path, and `Recover` reopens the
working copy still bound to the document path, so saving a recovered document
overwrites the document rather than the copy. A session recovered from a working
copy starts dirty, because its content is not yet its file's content. Session
ownership of the undo stack stays with `DocumentCommandHistory` in Application;
the composition root pairs them for the editor.

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

The catalog is filled in batches rather than at once. The first build shipped
`Image Source` and `Save Image` in Input/Output, and `Gaussian Blur` and `Resize` as
the Filter and Transform pair that proves the pipeline. The Aries migration then
added the common single-image operations in four batches. The first took `Colour
Conversion` and `Crop` in Transform, `Median Blur` in Filter, and `Threshold` in
Threshold. The second took the smoothing and pyramid nodes: `Blur` and `Bilateral
Filter` in Filter, `Adaptive Threshold` in Threshold, and `Pyramid Down` and
`Pyramid Up` in Transform. The third took the derivative and edge nodes: `Sobel`,
`Scharr`, and `Laplacian` in Filter, which measure how fast an image changes, and
`Canny` beside them, which is the one of the four whose result is a map rather than a
measurement. The fourth filled the Morphology category the design has listed since
the first release: `Erode` and `Dilate`, which thin and thicken by a structuring
element, and `Morphology Ex` beside them, whose operation picks the opening, the
closing, the outline, or one of the two hats. The first two batches were chosen the
same way: each node says what it
does on a frame the test writes itself, a uniform field or a step with a couple of
marked pixels in it, so none of them needs a curated sample image to be
regression-tested. The third waited on the one decision its nodes share, which is the
depth a signed result is reported in, and it answers that by reporting the absolute
value of the derivative in the layout the input was given: no node consumes a signed
frame and the preview refuses to draw one, so a frame that held the sign would be a
value nothing could read and nobody could see. With that settled the four read the
same hand-written frames as the batches before them, a step being what a derivative is
defined on, and each departure the batch makes from the reference — the axis the
`Scharr` node declares, the apertures the `Sobel` and `Laplacian` nodes offer, the
thresholds the `Canny` node refuses — is recorded in PL-2026-003. The fourth needed no
such decision: a structuring element reads the neighbours of a pixel and writes its
type back, so those nodes accept every layout a frame can hold and report the one they
were given, exactly as the two pyramid nodes do, and the features they are tested on
are the same hand-written ones — a step, a single brighter pixel, and a single gap —
because what an element can remove or fill is measured in pixels. Its departures are
recorded in PL-2026-003 as well: the kernel is a shape and the side of a square rather
than a kernel the user assembles, the anchor is always the centre, and the operation is
one of five options rather than a number. The fifth filled the Draw category, which the
table has also listed since the first release: `Draw Rectangle`, `Draw Line`, and `Draw
Circle`, which are the only nodes that change a frame without reading it as a
measurement. It exists for one decision, and it is how a colour is a parameter: the
inspector holds numbers, so a colour is three of them, named after the components a
drawing call reads in order — `blue`, `green`, and `red` — and a frame of a single
channel takes the first, which is what a scalar means to such an image rather than a
rule this layer invents. The default is white, so a mark on a mask needs nothing set,
and `filled` is its own switch rather than OpenCV's width of minus one, because a
parameter whose value says another one does not apply is a parameter that hides a
decision; the line node declares no such switch, since a line has no inside. Each node
copies the frame it was given and marks the copy, because a frame in this build has one
owner and marking the frame it was given would change a value another node may still
read. A shape that reaches past an edge is drawn up to it, which is what marking a
region at the border means, and because the call reads pixels and writes the same type
back, every layout is accepted and reported, as the two pyramid nodes and the
morphology nodes do. The tests state the whole result of a shape as a picture of the
marked pixels, so the rasterisation and the clipping are what is asserted rather than
described.
What the batches leave out names its reason in the issue that
migrated them: `Filter2D`, `Normalize`, the point scaling node, and the channel nodes
wait on a kernel representation, on a decision about masks and depths, on a consumer
of its own, and on multi-port nodes respectively, and `Draw Contours` — the other node
the table's Draw row names — waits on the `Contours` value ADR-0003 defers, which is
also what the contour family waits on.
The two file-backed nodes — `Image Source` and `Save Image` — are the ones that
name a file, so they are the nodes a working directory is resolved for (5.4): each
declares a required `path` parameter, the save node additionally declares the
`overwrite` boolean that defaults to off, and the path names a file relative to the
folder that holds the document (ADR-0012). The remaining rows are the catalog this
design aims at rather than a list of what exists, and each arrives with the curated
regression images its own entry requires. That is also what the operations whose
result depends on real image content — contours and template matching above
all — are waiting for.

Every migrated node receives output-oriented regression tests, and a node whose result
depends on real image content receives them against curated sample images. Migration
moves algorithm intent and behavior, not the old GraphX controls, Aries view models,
or serialization format.

## 9. Configuration, diagnostics, and security

The host reads `appsettings.json` beside the executable, which ships safe
defaults, and an optional `appsettings.user.json` that overrides them on one
machine. Each section binds to the options record of the layer that honors it —
`ExecutionOptions` in Application, `FramePreviewOptions` in OpenCv, and
`WorkflowAutosaveOptions` in Persistence, which owns the working copy the
interval refreshes. A key the file omits keeps the default that record declares,
so a partial file stays valid. Plugin search locations arrive with the plugin
loader and its ADR.

Every section is validated at startup, before any service reaches the shell. A
value outside the range the runtime can honor is reported as `VW-CONFIG-001` in
a modal message and in the log, and startup stops: clamping a limit the user
wrote down would make a misconfiguration indistinguishable from a defect.
`VisionWeaveSettings` performs that binding and checking, `VisionWeaveServices`
registers the node catalog, the validator, and the resolved options, and `App` is
the only project that references the host stack of ADR-0007. Node definitions
enter the catalog through one registration path, so a duplicate node type
identifier fails composition rather than shadowing the definition that arrived
first.

Structured logs use named fields rather than interpolated prose. Startup records
the resolved settings and the empty document it opened; a rejected setting is
logged once where it is reported. Run-boundary logs add `OperationId`,
`WorkflowId`, `NodeId`, `NodeTypeId`, `DiagnosticCode`, and elapsed milliseconds
when that path is wired. Logs never include full image payloads, credentials,
tokens, or unsafe path contents. A node failure is logged once at the execution
boundary with the original exception as context; the UI receives a safe
diagnostic.

Plugins are disabled by default in the first release. `PluginSdk` is still
implemented as a small stable contract so built-in providers exercise the same
registration path, and the host does not reference it while nothing loads a
plugin. External loading is not exposed until its dedicated ADR and
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
| Application unit tests | Topological order, dirty-subgraph selection, cancellation, a grace period and a late executor reached by moving the clock the run was given, the durations a run reports, branch blocking, document command history, undo grouping, refused edits, settings validation, selection-safe validation projection. |
| OpenCV integration tests | Expected pixels / geometry for each migrated node, disposal and cache behavior, preview limit validation. |
| Persistence integration tests | Save/load round trip, malformed document rejection, migrations, missing-node placeholders, autosave policy validation. |
| Architecture tests | Dependency direction, no WPF/OpenCV/host stack reference in Domain, and no host stack reference in any core assembly. |
| Shell tests | The six documented regions and their named elements, token values and brush aliases, contrast of text and of the focus ring, no colour literal outside the token dictionary, every bound path resolvable through a public member, every declared key used and declared before it is reached for, the canvas wiring of items, wires, commands, and shortcuts, the inspector's field per declared kind and the gesture that applies what was typed, prompt questions and their three answers, status transitions including the four ways a run can end, and announcements by severity. |
| UI smoke tests | The real window, drawn with the shipped theme: placing a node from the catalogue, connecting two ports, selecting, deleting, undo, redo, and the surface a refused connection leaves unchanged; editing a parameter, reading the condition a refused value earned, undoing and redoing one parameter edit as one unit, saving, opening the file again over unsaved changes and answering the prompt; opening a document this build must not write back, which is shown read-only and changes nothing when a gesture reaches it; and running a saved workflow over a real image — three nodes placed and wired by the gestures the canvas offers, the output file read back from the document's own folder, the newest published image drawn in the preview region with the note that stood in for it gone, the counts of the ledger flat, and a run stopped while a node is executing, which keeps the preview the source had already published, writes nothing, reports no condition, and returns both gestures to the state they started in. |

Tests use xUnit and Shouldly. Test names use the form
`Member_condition_expected_result`, e.g.
`ConnectPorts_incompatible_image_and_contours_returns_port_diagnostic`.

Runtime tests are deterministic about time as well as about order: the runner is
given a clock the test moves by hand, so a cancellation grace period expires, a
completed level is shown to have taken its wait off the clock again, and a
quarantined executor that finishes late is awaited without a real delay anywhere
in the test.

The shell's markup is linked into `VisionWeave.App.Tests` as data, so regions,
styles, and bindings are checked against the files the application ships without
creating a window; a WPF element that a test genuinely needs is built on a
single-threaded-apartment thread, the way the shell builds it. The smoke test goes
one step further and builds the real window over the application's own resources,
so the templates, the bindings, and Nodify's containers and connectors are covered
as they are drawn rather than as they are declared. It is also the only place a
document change can be checked against a binding's thread affinity: a window is a
dispatcher object, so an open that announced its document from a worker thread would
leave the surface showing the document it replaced, which is a failure no headless
test can see. Running belongs there for the same reason: the frames a run converts
are converted off the window's thread, and only a real window shows that the image
it draws arrives through the dispatcher and outlives the frame it came from. The
flow runs over a real image file written into the temporary document folder, reads
the written output back, stops a run while a node is held open, and asserts the
ledger is flat afterwards, which is what a leaked frame would not be.

The composition root is covered by `VisionWeave.App.Tests`, which resolves every
registered service headlessly and refuses to start on a rejected setting. The
application is additionally started by hand once with the shipped settings, which
must open the documented regions, and once with a settings file that holds
rejected values, which must report `VW-CONFIG-001` and exit without opening a
document.

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
| [ADR-0007](../adr/0007-host-composition-and-configuration.md) | Host composition root, the dependency-injection, configuration, and logging stack, startup validation, and the boundary that keeps those packages out of the core assemblies. | Accepted |
| [ADR-0008](../adr/0008-async-ui-command-boundary.md) | The single asynchronous command and error boundary: expected failures as diagnostics, cancellation as an outcome, one presenter for user-facing messages, and the message-box exception at startup. | Accepted |
| [ADR-0009](../adr/0009-editor-session-orchestration.md) | The host-layer editing session that composes the document session, the command history, the selection, and the validation projection, and the transitions it owns. | Accepted |
| [ADR-0010](../adr/0010-diagnostic-targets.md) | The closed hierarchy of diagnostic targets narrower than a node, how the validator attributes them, and how the projection answers per port, parameter, and connection. | Accepted |
| [ADR-0011](../adr/0011-resource-references-and-port-schema-snapshots.md) | Resource references and remembered port schemas as typed contracts: what a document edit they are, what the constructors guarantee, and how the reader and writer treat an entry they cannot represent. | Accepted |
| [ADR-0012](../adr/0012-file-access-and-the-working-directory.md) | What a node's `Path` parameter names, the working directory a run resolves it against, how much of the file system one run may reach, and how a save treats a file that is already there. | Accepted |
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
