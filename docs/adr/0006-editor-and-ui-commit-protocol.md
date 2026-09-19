# ADR-0006 Editor scope and UI commit protocol

- **Status:** Proposed
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

The canvas must stay replaceable and testable while the shell, theming, and
property editing come from a separate component library. The detailed design
requires that the editor never decides connection legality, that rejected
connections are recovered cleanly, and that ViewModels never mutate the domain
directly. Aries demonstrates the opposite arrangement: blocks, layout state, and
serialization were bound to the canvas control.

## Decision

1. **Nodify 7.3.0 is the editor control only.** Its types are referenced only by
   `VisionWeave.App`. The application domain, the Application layer, and
   persistence never see a Nodify type, a `NodifyEditor`, or a connector
   control.

2. **WPF UI 4.3.0 owns the shell and common controls,** including navigation,
   dialogs, theming, notifications, and the property-editor controls
   (`NumberBox`, `ComboBox`, `ToggleSwitch`, and the path picker). Editors are
   selected from the parameter schema by `ParameterEditorTemplateSelector`; a
   node definition may not ship a WPF template.

3. **One-way commit protocol.** UI intent becomes an Application command; only
   an accepted command produces a graph delta; the projection then refreshes
   from authoritative state. A ViewModel property setter never mutates the
   domain. Every command reports its outcome as diagnostics rather than throwing
   into the binding layer.

4. **Pending connections are visual only.** Nodify's pending connection is not a
   graph edit. Acceptance happens in `ConnectPorts`, which evaluates direction,
   type compatibility, multiplicity, and cycle rules. A rejected connection is
   removed through the interaction completion callback and surfaced with the
   diagnostic produced by Application, so the UI never re-implements the rule.

5. **Undo, redo, and edit granularity.** Application owns a single undo stack.
   Node move, multi-move, paste, and delete are each one atomic command and one
   undo unit. A parameter edit is one unit per node and parameter, coalescing
   edits that arrive within a short configured window.

6. **Revisions drive execution invalidation.** A semantic edit (parameters,
   nodes, connections) increments the document revision and cancels the active
   run; a layout-only edit increments no revision and does not cancel it.

7. **Automatic layout and edge routing are deferred.** Aries offered tree and
   circular layouts plus orthogonal edge routing through its GraphX fork.
   Nodify provides rendering and interaction, not layout algorithms. The first
   release keeps manual placement only and records the missing capability here
   instead of implying it. A layout service would be an Application-layer,
   unit-testable component added later.

8. **Composite and subgraph nodes are deferred.** Aries could embed a saved
   graph as a node. The first release ships a visual group node with no
   executor, and nested workflow execution is a separate future decision.

9. **Verification split.** ViewModel and command behavior is covered by
   automated tests that do not require a window. Canvas rendering, theming, and
   drag interaction are covered by a short manual checklist, which is recorded
   as a project operational constraint because the CI runner cannot assert
   visual output.

## Consequences

The canvas becomes replaceable and most UI behavior becomes testable without a
window. Two Aries capabilities — automatic layout and nested subgraphs — are
explicitly absent, so a workflow with hundreds of nodes must be arranged by
hand, and graph reuse means copy and paste rather than composition.

## Alternatives considered

Keeping GraphX was rejected because its forked connection-point metadata and
layout coupling are what made the Aries editor hard to change. A custom canvas
was rejected because selection, connectors, zoom, and minimap behavior are
mature in Nodify. Editing the domain directly from ViewModels was rejected
because it makes connection legality untestable and untraceable.

## Standards impact

Implements the directed-dependency rule linked from
[the standards reference](../standards-reference.md), and records the UI
verification limitation as a project constraint. No deviation is required.
