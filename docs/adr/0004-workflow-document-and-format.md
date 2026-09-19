# ADR-0004 Workflow document, executable snapshot, and `.vwflow`

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

The detailed design requires a document that can be opened even when it is not
executable: it may contain a cycle, a connection whose port no longer exists, or
a node whose plugin is absent. The same design also requires an immutable,
validated snapshot for execution, cache keys that include the node definition
version, and migrations that never silently drop unknown content. A single type
cannot satisfy both requirements, and the Aries format failed precisely because
it stored live UI objects without a schema version or an unknown-node path.

## Decision

1. **Editing and execution use different types.**
   - `WorkflowDocument` (Domain) is the persisted, editable form. It may be
     structurally invalid and always remains loadable, renderable, and savable.
   - `WorkflowSnapshot` (Application) is built from a document plus a resolved
     node-definition catalog. It is immutable and validated. Only snapshots are
     executed, cached, or passed to executors.
   - Structural validation therefore returns diagnostics and never prevents a
     document from being loaded or displayed.

2. **Validation belongs to Application.** `WorkflowDocument` enforces only what
   makes it coherent (identity, revisions, referential integrity of its own
   collections). Direction, type, multiplicity, and cycle rules are evaluated by
   the Application validator, which reports stable diagnostic codes. `Domain`
   does not reference OpenCvSharp, presentation assemblies, or the catalog.

3. **`.vwflow` is versioned JSON.** `schemaVersion` is a document-level integer.
   The document stores workflow state only, never UI templates, native handles,
   cache entries, or machine-specific absolute assumptions.

4. **Every node entry records the definition version it was saved with.**
   `typeId` is a `NodeTypeId`; `typeVersion` is the `NodeDefinition.TypeVersion`
   in effect at save time. Cache keys and run summaries use `typeId` plus
   `typeVersion`. A document without the field is treated as version 0 and
   migrated on load.

5. **Definition evolution is migrated explicitly.** Each node definition may
   ship `INodeDefinitionMigration` implementations keyed by source version.
   Parameter renames, port renames, and removals must be declared there.
   Migration is forward-only and idempotent. A node whose type, version, or
   migration is unavailable becomes a non-executable placeholder rendered from
   its `portSchemaSnapshot`; its raw JSON, `extensionData`, and all connections
   are preserved verbatim so the workflow is restored when the definition
   returns.

6. **`portSchemaSnapshot` is a rendering fallback, never a second source of
   truth.** For a known node type the resolved catalog definition wins, and a
   snapshot disagreement is reported as a diagnostic. The snapshot is consulted
   only to render a placeholder node and to explain a saved connection whose
   port is unknown.

7. **Documents store references, not machine fingerprints.** `resources` records
   the stable reference a node depends on (`kind`, `path`, optional
   `expectedSha256` supplied by the user). The runtime computes the actual
   fingerprint (length, last-write time, content hash) when it builds a
   snapshot; the fingerprint participates in cache keys but is never written
   into the workflow document. Replacing a file with different content therefore
   invalidates cached outputs instead of silently reusing them.

8. **Document metadata is explicit.** A document records `documentId`, `name`,
   `createdUtc`, `modifiedUtc`, `appVersion`, `revision`, `requiredPlugins`, and
   `extensions`. `requiredPlugins` lets the loader report missing providers
   before the user inspects the canvas. Unknown top-level and per-node fields are
   preserved on save.

9. **Forward-version policy.** A document whose `schemaVersion` is newer than the
   running application supports opens read-only and is never rewritten
   destructively. Saving is atomic: a temporary file in the destination
   directory followed by replacement, with a separate recoverable autosave copy.

10. **Layout state has an explicit boundary.** Per-node canvas position is
    document state. Viewport, zoom, panel sizes, theme, and window placement are
    local user preferences and are stored outside the document.

## Consequences

The loader has two phases exactly as the detailed design describes, and the
placeholder path is a first-class requirement rather than an error path. Node
definitions carry a version that must be maintained deliberately; forgetting to
bump it is a review concern, and cache correctness depends on it.

Storing references instead of fingerprints keeps documents portable between
machines, at the cost of computing fingerprints when a snapshot is built.

## Alternatives considered

A single mutable `Workflow` type that refuses invalid states was rejected
because it cannot represent a loaded document that needs repair. Identifying
nodes by CLR type name was rejected because it reintroduces the Aries failure
mode. Storing file fingerprints in the document was rejected because the same
workflow would then diff differently on every machine.

## Standards impact

Implements the directed-dependency and stable-identifier rules linked from
[the standards reference](../standards-reference.md). No deviation is required.
