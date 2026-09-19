# ADR-0012 File access and the working directory

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

The catalog's first two nodes consume and produce frames that only a test can
supply, because no node reads or writes a file. Section 8 of the detailed design
names the pair that changes that — Image Source and Save Image — and pins them to
"local file paths only in MVP", while `ParameterKind.Path` has existed since the
port contracts without a rule saying what a path value means. PL-2026-005 records
the gap as "the path-parameter kind and the work-directory rules the design
defers".

Two decisions have to be made before a node can touch a file. The first is what a
stored path names. Section 7 requires a `.vwflow` to hold "only portable workflow
state, not WPF templates, native handles, local cache entries, or absolute
user-machine assumptions", so a document that stores `C:\Users\ana\plate.png`
contradicts the format's own contract, and a document that stores `plate.png` is
only meaningful if something says what it is relative to. The second is how much
of the file system one run may reach. A node that writes a file is also the first
node with an external side effect, so it is the first whose result a later cache
must not reuse (section 5.2).

Nothing else in the product answers these questions yet: there is no output
directory setting, no resource picker, and no migration of anything already
stored, because no stored document names a file at all.

## Decision

1. **A `Path` parameter names a file relative to the workflow's own directory.**
   The working directory of a run is the directory that holds the document being
   run. A document that has never been saved has no such directory, so a run of it
   is refused before any node starts, and the shell reports that as a condition
   naming what to do about it. Falling back to the process's current directory
   would make one document read different files depending on how the application
   was launched, which is exactly the machine assumption the format forbids.

2. **The working directory travels with the run, never with the application.**
   `NodeExecutionEnvironment` in `Contracts` carries it, the `ExecutionPlan` of one
   run carries the environment, and the runner hands it to every executor in the
   request the node receives — the "non-secret execution options" section 5.1
   already declares a request to contain. No executor reads a process-wide
   directory, so two runs of two documents in one process cannot resolve the same
   relative path differently from what the user is editing.

3. **Only a relative path inside the working directory is accepted.** A rooted
   path, a blank path, a path that carries a volume separator, and a path that
   escapes the directory through `..` are all refused, and each refusal names the
   rule it broke. This is what keeps a document portable — the same workflow reads
   its own folder after the folder is copied, renamed, or checked out elsewhere —
   and it makes the file access of a run reviewable by reading the paths in the
   document rather than by trusting the nodes.

4. **Resolution is a pure contract that never throws.** `WorkDirectoryPath.TryResolve`
   turns a declared path and a working directory into an absolute path or into a
   refusal, and treats every unusable input the same way: a missing working
   directory, a path that names a directory, an illegal character, and a path too
   long for the platform are refusals like any other, because a node reports an
   expected condition as a diagnostic rather than as an exception. Executors never
   compose a path themselves, and they never pass a declared path to a file API.

5. **A refused path fails the node that declared it.** The failure carries the
   diagnostic a node already reports for any other unusable parameter, so a run
   keeps one failure vocabulary, the branch downstream of the node stays blocked,
   and the shell needs no second presentation for a path problem.

6. **A save may not replace a file the document has not acknowledged.** The Save
   Image node declares an `overwrite` boolean that defaults to false and refuses an
   existing destination until the document sets it, because a run that silently
   destroys the file it named is the one outcome a first-release user cannot undo.
   A write that is allowed goes to a temporary name in the destination directory
   and then replaces the destination, so a failed or cancelled write never leaves
   a half-written image where a valid one used to be.

7. **Nothing else about paths enters the document, and none of it is configurable
   in this release.** There is no per-node directory, no output-directory setting,
   and no absolute-path opt-in: one directory per document is one answer to "what
   may this workflow touch", and a second one would be a second thing to explain,
   validate, and get wrong. The capability that a real need justifies — an output
   directory, or an absolute path the user opts into explicitly — is recorded as
   PL-2026-018 rather than built speculatively.

8. **The save node is a sink and is excluded from caching.** It declares no output
   port, and its result is a file rather than a value: when result caching arrives
   (section 5.2), a cached save would report a write that never happened, so the
   node is marked as side-effecting rather than keyed by its parameters. No cache
   exists yet, so this is recorded here for the package that adds one.

## Consequences

A document stays portable, a run provably touches only files beside the document
it runs, and the shell can explain every refusal in the user's own terms, because
the refusal is written where the path was.

Four costs are accepted. A user cannot name a file outside the document's folder,
not even deliberately, which is a real convenience given up for reviewability and
recorded as PL-2026-018. A workflow cannot be run before it is saved, which is a
new first step the shell has to explain. Because the base directory is derived
from the document's path, saving a workflow somewhere else changes what its
relative paths name — deliberate, since a workflow is meant to travel with its
data, but it means a copy of a document is a copy of a job, not of a file. And
because a save refuses an existing destination by default, the first execution of
a workflow whose output file is already there fails with a message instead of
overwriting it, which is the safe direction to fail in but is a failure a user
will meet.

## Alternatives considered

Allowing absolute paths was rejected because it makes a document describe one
machine, which section 7 forbids, and because it turns "what does this workflow
touch" into a question only a careful reading of every path can answer. Resolving
a relative path against the process's current directory was rejected because the
same document would read different files depending on how it was launched. A
configured output directory was rejected for this release because no capability
needs it yet: a save node names its own destination, and a second directory would
exist only to move that destination somewhere the user did not write. Putting the
resolution rule beside the executors in `OpenCv` was rejected because the rule is
then reachable only from the layer that runs algorithms, and the shell could not
report a refusal without duplicating the rule. Making the directory a property of
each node was rejected because one run needs one answer to what it may touch, and
a per-node base is a second place for that answer to drift. Writing the
destination directly instead of through a temporary name was rejected because a
cancelled run would leave a truncated image where the previous one was.

## Standards impact

The environment record and the resolution rule live in `Contracts`, which
continues to hold no WPF, Nodify, or presentation type; `OpenCv` continues to
reference only `Contracts`, so the rule reaches an executor without a new
dependency edge; `Application` continues to reference `Domain` and `Contracts`.
The file APIs used are the platform's own, and no deviation is required.
