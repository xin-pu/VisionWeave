# Pre-UI Hardening and Dark-Amber Visual Direction

## Purpose

This plan orders the work required before and during the first WPF node-editor
implementation. It keeps UI work from becoming a second workflow model and
gives parallel contributors small, reviewable ownership boundaries.

The plan complements the [Development plan](development-plan.md). When the two
documents differ in detail, the stage ordering and architectural constraints in
the development plan take precedence.

## Reference boundary: OpenCMIS informs, it does not define

OpenCMIS is a local interaction reference, not a dependency, template, or
design-system source. VisionWeave may reuse these product-level observations:

- a stable application shell that makes the current context easy to find;
- a dominant work surface with secondary information kept out of the way;
- persistent, concise operational state and non-blocking feedback; and
- deliberate enablement of risky operations rather than ambiguous controls.

VisionWeave must not copy OpenCMIS source code, XAML, icons, layouts, color
values, navigation pages, controls, screenshots, or device-management
vocabulary. Its primary experience is a document-oriented workflow editor, not
a collection of hardware-management pages.

## Intended application information architecture

The first editor shell has one document-centered workspace. It is intentionally
smaller than OpenCMIS's multi-page navigation model.

```text
+--------------------------------------------------------------------------------+
| App menu / document commands / run state                                       |
+----------------+-----------------------------------------+-------------------+
| Node catalogue | Workflow canvas                          | Inspector         |
| Search         | Tabs or document title                   | Selection details |
| Categories     | Pan, zoom, selection, validation marks   | Parameters        |
|                |                                         | Diagnostics       |
+----------------+-----------------------------------------+-------------------+
| Status: document state | background operation | run outcome | safe diagnostic |
+--------------------------------------------------------------------------------+
```

The canvas is the primary surface. The catalogue creates *intent* only; the
Application layer validates and commits it. The inspector edits the selected
document object through the same command/history boundary. The status area
reports durable state, while transient success, warning, and failure feedback
uses an accessible snackbar or equivalent presenter.

## Dark-amber design tokens

These are semantic design targets, not a directive to create WPF resource
dictionaries before the prerequisites below are complete. Their names describe
meaning so a later light theme or high-contrast theme can replace values without
changing control templates.

| Semantic token | Initial dark-amber value | Use |
| --- | --- | --- |
| `Color.Background.Canvas` | `#111315` | Application and canvas base. |
| `Color.Background.Surface` | `#1A1D20` | Side panes, cards, menus. |
| `Color.Background.SurfaceRaised` | `#24282D` | Floating and selected surfaces. |
| `Color.Border.Subtle` | `#363B42` | Separators and resting input borders. |
| `Color.Text.Primary` | `#F5F2EA` | Primary text and essential icons. |
| `Color.Text.Secondary` | `#B9B5AC` | Supporting text. |
| `Color.Accent.Primary` | `#F6B73C` | Primary action, selection, keyboard focus. |
| `Color.Accent.Hover` | `#FFD166` | Hover and emphasis. |
| `Color.Accent.Pressed` | `#D99717` | Pressed state. |
| `Color.State.Success` | `#69C48B` | Successful, non-primary status. |
| `Color.State.Warning` | `#F6B73C` | Warning; pair with icon and text. |
| `Color.State.Danger` | `#FF7070` | Failure and destructive actions. |
| `Color.State.Info` | `#79B8FF` | Neutral operational information. |

Amber is an attention color, not the only way to encode state. Text, icon,
shape, and enabled/disabled treatment must distinguish status independently of
color. Normal-size text must meet a 4.5:1 contrast ratio against its rendered
background; future theme work must add automated or documented contrast checks
for token pairs and keyboard-focus visibility.

## Ordered work packages

Each package becomes one issue and one short-lived branch. Do not start a
dependent package until its predecessor has merged into `master` and passed the
repository quality gate.

| Order | Proposed issue / branch | Goal and repository ownership | Depends on | Acceptance evidence |
| --- | --- | --- | --- | --- |
| 1 | Existing #11 / `feat/11-async-ui-command-boundary` | Finish the async UI command and error boundary. Own `src/VisionWeave.App/Composition`, app command infrastructure, and matching app tests. Map unexpected failures to safe diagnostics and preserve cancellation semantics. | Current `master` | Commands expose running/can-execute state; cancellation is not reported as an error; unexpected failures reach one presenter and structured logs; tests cover success, failure, cancellation, and re-entry. |
| 2 | `feat/editor-session-orchestration` | Add an Application-facing editor-session facade for new/open/save/autosave/recovery, document dirty state, selection, command history, and validation projection. It owns no WPF or Nodify types. | 1 | Headless tests prove new/open/save/recover, dirty transitions, undo/redo, diagnostic projection, and an unsupported document remains read-only. |
| 3 | `feat/diagnostic-attribution-contract` | Close the diagnostic-attribution gap before visual markers exist. Define stable optional targets for document, node, port, parameter, and connection diagnostics; adapt validation output without leaking UI objects. | 2 | A validation error can be attributed to each supported target; unknown or stale targets degrade safely to document-level diagnostics; serialization compatibility tests pass. |
| 4 | `feat/resource-schema-contracts` | Resolve the deferred resource-reference and port-schema needs required for file nodes and missing-node presentation. Preserve forward-compatible opaque data only where typed semantics are still unknown. Delivered as [ADR-0011](../adr/0011-resource-references-and-port-schema-snapshots.md). | 3 | Tests prove typed file resource references, schema snapshots, unknown-node preservation, and deterministic read/write behaviour. |
| 5 | Existing #22 / `feat/22-editor-shell-theme-foundation` | Implement only the WPF shell and semantic theme resources: dark-amber tokens, layout regions, status/snackbar presentation, keyboard focus, and design-time sample data. No editable Nodify canvas yet. Delivered as the five named regions, `Themes/Tokens.xaml` plus `Themes/Shell.xaml`, `ShellStatus`, and `SnackbarNotificationPresenter` in section 6.5 of the detailed design. | 1 | App launches to the documented regions; no business rule lives in code-behind; theme-resource tests or inspection evidence cover key states and contrast; App tests remain green. |
| 6 | `feat/nodify-canvas-projection` | Project the document to generic nodes, ports, and connectors. Implement pan, zoom, selection, add/delete/connect intents, rejection rollback, and undo/redo synchronization. | 2, 3, 5 | UI smoke test proves add, valid/invalid connect, selection, delete, undo, redo, and visual rollback after a rejected command. |
| 7 | `feat/inspector-document-ux` | Add generic parameter editors, diagnostics panel, dirty/save/open/recovery prompts, unsupported-document view, and node-catalogue search. | 4, 6 | UI smoke test edits a parameter, shows a targeted diagnostic, saves/reopens, and blocks edits to an unsupported document. |
| 8 | `feat/first-runnable-image-workflow` | Deliver file-image input, one transform (resize or blur), image output, managed preview, run/cancel/status, and working-directory policy. | 4, 7 | A golden workflow opens, validates, runs on a real image, displays a managed preview, writes output, cancels safely, and leaves zero native-frame leases. |

## Agent handoff contract

For each work package, the implementing agent must receive the issue link, this
plan section, affected ADRs, and the exact branch name. The agent should change
only the folders named by its package unless the issue explicitly expands the
scope. A reviewer should verify the following before merge:

1. The Application/Domain/Persistence layers contain no WPF, Nodify, or
   WPF-UI types.
2. Every user gesture becomes an application command or query; visual state is
   refreshed from committed document state rather than becoming authoritative.
3. Failure, cancellation, and validation are distinguishable and safely
   presented.
4. Tests demonstrate both the newly accepted operation and rejection/rollback
   behaviour where an edit can fail.
5. The complete repository gate passes:

```powershell
dotnet build VisionWeave.slnx --no-restore
dotnet test VisionWeave.slnx --no-build
dotnet format VisionWeave.slnx --verify-no-changes --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ProjectDocuments.ps1
```

## Gate to begin editable canvas work

Begin package 6 only after packages 1 through 5 are merged and the following
facts are demonstrably true:

- the app has a single safe asynchronous error boundary;
- editor-session actions have headless contracts and tests;
- diagnostics can point to stable document elements;
- the theme is semantic, keyboard-visible, and independent from business
  logic; and
- the shell is an original VisionWeave layout, not an OpenCMIS page clone.

## Related records

- [Development plan](development-plan.md)
- [Detailed design](visionweave-detailed-design.md)
- [ADR-0006: editor and UI commit protocol](../adr/0006-editor-and-ui-commit-protocol.md)
- [ADR-0007: host composition, configuration, and logging stack](../adr/0007-host-composition-and-configuration.md)
- [ADR-0008: async command and error boundary](../adr/0008-async-ui-command-boundary.md)
- [ADR-0009: editor session orchestration](../adr/0009-editor-session-orchestration.md)
- [ADR-0010: diagnostic targets narrower than a node](../adr/0010-diagnostic-targets.md)
- [ADR-0011: resource references and remembered port schemas](../adr/0011-resource-references-and-port-schema-snapshots.md)
- [Project improvements ledger](../ledger/project-improvements.md)
