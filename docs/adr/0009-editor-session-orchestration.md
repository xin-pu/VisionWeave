# ADR-0009 One editing session for the shell

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

Every piece of an editing session exists and is tested, and nothing composes
them. `WorkflowSession` binds a document to its file and owns the dirty flag, the
read-only state, save, autosave, and recovery. `DocumentCommandHistory` owns the
single undo stack of one document. `ValidationProjection` answers for a selection
at one document revision. Each sits in the layer that can see what it needs: the
session is in Persistence, because that is the only layer that sees both the
document and the file, and the history and the projection are in Application.

`Application` references only `Domain` and `Contracts`, and `Persistence` does the
same, so neither layer may reference the other. No existing layer can therefore
present those three pieces as one session, and a shell that held them separately
would have to re-implement the commit protocol of ADR-0006 — an accepted command
produces the delta, the projection then refreshes from authoritative state, and a
revision drives invalidation — at every call site, with the document revision
checked by hand.

## Decision

1. **The composition belongs to the host layer.** `EditorSession` lives in
   `VisionWeave.App.Sessions` and is the one object the shell presents. The host
   layer is the only one allowed to reference `Application` and `Persistence`
   together, and ADR-0007 already makes it the place that knows which
   implementations this build loads. The session holds the current
   `WorkflowSession`, its `DocumentCommandHistory`, the selected node instances,
   and the current `ValidationProjection`.

2. **Every transition reports a diagnostic instead of throwing.** New, open,
   recover, save, save-as, autosave, edit, undo, and redo each return either the
   session they adopted, the outcome of the attempt, or the diagnostics that
   explain a refusal, so no binding ever has to interpret an exception to explain
   itself. A refusal names a stable code: `VW-FILE-004` when a document with no
   file is asked to save, `VW-FILE-002` when the file is read-only because this
   build cannot migrate its schema, and `VW-FILE-001` when the destination cannot
   be written. The history's own refusals, `VW-EDIT-001` and `VW-EDIT-002`, pass
   through from Application unchanged.

3. **A session swap replaces the history, the projection, and the selection.**
   The history holds the commands of the document it was built for and a
   projection answers for one revision, so neither survives a new document, an
   open, or a recovery. They are rebuilt from the adopted document in one place,
   which is what makes undo incapable of reaching into a document it never saw.

4. **Opening is cancellation-aware.** A file read cannot be interrupted, so the
   session checks the token after the read returns and before it adopts the
   result: a stopped open leaves the document the shell is editing in place
   instead of replacing it with one nobody asked for. The read itself still runs
   off the calling thread, under the command and error boundary of ADR-0008.

5. **The projection is refreshed on every accepted commit.** A refused edit
   changes neither the document nor the projection, so a node badge can never
   describe a revision the document has already moved past.

6. **Selection is intent, and the projection decides what still applies.** The
   selection is stored as the identifiers the shell reported, including ones the
   document no longer holds; asking the projection for a selection yields the
   diagnostics that still match plus the document-level ones, so a panel cannot
   call a selection runnable while a graph-wide failure blocks the run.

7. **The view model keeps no mutable state.** It reads the session and derives
   its strings from it, and it re-raises those strings whenever the session
   reports a change, so the title, the summary, and the dirty state cannot drift
   from the state they describe. Nothing in the shell replaces the session object
   itself any more; the session replaces the document inside it.

8. **Path failures are classified once.** Opening and saving share one predicate
   for the failures a path produces before any content is read or written, so the
   same missing folder or denied permission is reported as the same diagnostic
   whichever direction the file was travelling.

9. **No WPF or Nodify type appears in the session**, so a whole session — new,
   open, save, recover, dirty transitions, undo and redo, and the failure paths —
   is covered by tests that never open a window.

## Consequences

The shell gets one authoritative document state, the commit protocol of ADR-0006
has a single implementation, and the representative command of ADR-0008 becomes a
thin caller that runs the read and reports what the session decided. Three costs
are accepted. The shell still has no file chooser, no status area, and no command
for new or save, because those belong to the editor-shell package; the session
API is usable but not yet reachable from the window. The session is host-layer,
so reusing it outside WPF — a command-line or batch entry point — would first
need it moved behind an Application-owned port, which is not justified while the
window is its only consumer. And one session means one document per shell until a
document-tab model exists.

## Alternatives considered

An Application-owned document port with the session in Application was rejected
for now: it would move file and schema logic across two layers to serve a second
consumer that does not exist, and it would leave `WorkflowSession` split between
the layer that owns it and the layer that describes it. Making the view model the
composer was rejected because the commit protocol would be re-implemented for
every surface that shows the document, and the same state would then live in two
places. Keeping the representative open command as the only entry was rejected
because it would make "the current document" mean two different things at once.
Moving `WorkflowSession` into Application was rejected because that layer cannot
reach the reader and the writer without adding a reference the architecture test
refuses or a port that duplicates the session.

## Standards impact

Implements the directed-dependency rule linked from
[the standards reference](../standards-reference.md): `Application` and
`Persistence` continue to reference only `Domain` and `Contracts`, and the new
composition lives where those references are already allowed. No deviation is
required.
