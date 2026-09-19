# ADR-0011 Resource references and remembered port schemas

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

ADR-0004 defines two version 1 fields that the document model never carried.
`resources` records the files a workflow reads — an image source points at a
plate on disk — with `kind`, `path`, and an optional `expectedSha256` the user
supplies. Each node entry carries `portSchemaSnapshot`, the ports its type
declared when the file was written, so a node whose provider is not installed
still draws the connectors its saved connections refer to.

The reader preserved both as extension fragments and the writer re-emitted them,
so a load and a save lost nothing, but nothing could read them either: a resource
reference could not reach the snapshot builder, and a placeholder node had no
snapshot to render. Re-emitting a preserved fragment also wrote it at the level
`extensionData` occupies rather than at the level the format defines for it, so a
rewrite moved the two fields. PL-2026-011 recorded that gap together with the
migration pipeline, and this record closes the modeling half of it.

## Decision

1. **A resource reference is a closed hierarchy in `Contracts`.**
   `ResourceReference` is abstract with two sealed records: `FileResourceReference`
   names a `Path` and an optional `ExpectedSha256`, and `UnknownResourceReference`
   keeps a `Kind` this build does not model together with the JSON it was read
   from. This is the shape `PortValue` and `DiagnosticTarget` already use in the
   same assembly, and it holds no presentation type.

2. **A resource is document data, so adding one is a semantic edit.**
   `WorkflowDocument.AddResource` commits semantically: the revision and the
   change count move, the session reports the document dirty, and autosave writes
   it. The alternative — treating a reference as metadata — would let a user add a
   resource, close the application, and lose the edit with no dirty state to warn
   them.

3. **A remembered port schema is metadata, so refreshing it is not an edit.**
   `WorkflowDocument.SetNodePortSchemaSnapshot` commits without moving the
   revision, the way a node move and a preserved unknown field do. The snapshot is
   a rendering fallback (ADR-0004 decision 6): the catalog definition wins
   whenever it exists, so recording what a definition once declared must not mark
   the document as changed. A load writes both through `Hydrate`, so reading a
   file still changes neither the revision nor the modification instant.

4. **A snapshot entry requires what identifies a port and makes everything else
   best effort.** `PortSchemaEntry` requires `PortId` and `Direction` and treats
   type, multiplicity, optionality, and display name as optional, because a
   snapshot is read to draw a connector that a saved connection already names. It
   does not reuse `PortDefinition`: that contract requires a port type and carries
   runtime concerns a snapshot must be able to omit.

5. **The constructors hold the invariants the reader and the writer rely on.** A
   file resource rejects a blank path, and a preserved fragment must be one
   complete JSON value, verified with `JsonDocument.Parse` when the reference is
   created. The writer hands a preserved fragment back through `WriteRawValue`
   without inspecting it again, so validating at construction is what keeps
   externally supplied text from failing at write time instead.

6. **The reader decides per entry and never fails a load it can complete.** A
   `resources` member that is not an array, a `file` resource that names no path,
   a snapshot that is not an array, and a snapshot entry without a usable port or
   direction are skipped with `VW-FILE-003`. A resource whose kind this build does
   not model is preserved as `UnknownResourceReference`, and a snapshot member
   whose value cannot be read is treated as one that was never recorded, so a
   document written by a build that knows more still opens with everything this
   build can represent.

7. **The writer emits both under their own fields, deterministically.**
   `resources` follows `requiredPlugins` in document order; each node's
   `portSchemaSnapshot` is written at the entry level, not inside `extensionData`,
   which corrects where a rewrite used to place it. Both are omitted when they are
   empty, enumerations are written by name, and a preserved fragment is written
   back exactly as it was read. A resource subtype this build does not know is a
   programming error and throws rather than being dropped silently.

8. **Typing the two fields needs no migration and no schema bump.** Both are
   schema 1 fields that ADR-0004 already defines, so a file written before this
   change carries them in the shape this build now reads, and no stored document
   needs rewriting. The first `IWorkflowMigration` pipeline stays a separate
   concern for the first schema version that actually needs one, tracked as
   PL-2026-017.

9. **Two things stay unmodeled on purpose.** The machine fingerprint of a
   resource stays runtime-only, because ADR-0004 decision 7 keeps the document
   holding a reference rather than a fingerprint. A member this build does not
   recognize inside `resources` or `portSchemaSnapshot` is not preserved
   individually, because a document newer than this build opens read-only and is
   never rewritten (ADR-0004 decision 9), so there is no newer content for this
   build to damage.

## Consequences

A placeholder node can be drawn from its remembered ports, and a resource
reference is now document state a caller can read, which is what a later run
input and cache key will be built from. Four costs are accepted. A resource now
makes a document dirty, which is deliberate but means a caller that adds one
without saving leaves unsaved state behind. The reader has to judge each entry
instead of copying text, so an incoherent entry is dropped where a preserved
fragment would have survived; the loss is bounded to entries that cannot be
represented at all, and each one is reported. `UnknownResourceReference` writes
back text it never parsed into a shape, so its value depends entirely on the
construction invariant of decision 5. And a snapshot read back may be missing
descriptive members the saving build did not record, which is why the catalog
definition, not the snapshot, remains the source of truth.

## Alternatives considered

Reusing `PortDefinition` for a snapshot entry was rejected because it requires a
port type and carries runtime members a snapshot must be allowed to omit, so
every snapshot of a partially known port would need a fabricated type. Keeping
the snapshot as raw JSON and parsing it where it is used was rejected because it
is the defect this record closes: no caller could read it without re-implementing
the format. Putting the fingerprint in the document was rejected by ADR-0004
decision 7 and would make a document invalidate itself when a file changed on
disk. Introducing `IWorkflowMigration` now, to convert the preserved fragments
into the typed fields, was rejected because both fields already belong to schema
1: a migration would run only against files this build reads correctly without
it, and would exist to justify the ledger entry rather than to convert anything.
Keeping the fields in `extensionData` and writing them from there was rejected
because it leaves two owners for one field name, and the level a preserved field
is written at is exactly what a rewrite used to move.

## Standards impact

Implements the layer rules linked from
[the standards reference](../standards-reference.md): the two hierarchies live in
`Contracts`, which continues to hold no WPF, Nodify, or presentation type;
`Domain` gains a reference to a `Contracts` namespace it already references;
`Application` continues to reference only `Domain` and `Contracts`. No deviation
is required.
