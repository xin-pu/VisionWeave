# ADR-0010 Diagnostic targets narrower than a node

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

`NodeDiagnostic` names a node and nothing else. Every condition the validator
reports about a port, a parameter, or a connection is therefore attributed to the
node that owns it: the message names the port, but nothing a program can read
says which port it meant. `ValidationProjection` consequently answers per node and
per document only, so a port or connection view model can inherit its owner's
severity and nothing more. A node whose kernel size is outside its declared range
marks its image connectors invalid as well, and a wire the validator rejected
cannot be found from its connection identifier.

The detailed design asks `PortViewModel` to expose its own validation state
(section 6.2) and records the missing attribution as PL-2026-013. Port and
connection markers are the first thing an editable canvas draws, so the contract
has to be narrower before those markers exist rather than after they have already
been built on a node-level answer.

## Decision

1. **A target is a closed hierarchy in `Contracts`.** `DiagnosticTarget` is
   abstract with three sealed records: `PortTarget`, `ParameterTarget`, and
   `ConnectionTarget`. Each names its element with the identifiers the document
   already carries — a node instance identifier, a port identifier, a parameter
   name, a connection identifier — so it holds no presentation type and no
   display text. This is the same shape `PortValue` already uses in the same
   assembly.

2. **A target refines the node scope; it does not replace it.** `NodeDiagnostic`
   keeps `NodeInstanceId` and gains an optional `Target`. A port or parameter
   condition sets both, so every consumer that groups by node — `DiagnosticsFor`,
   `SeverityOf`, `Select` — answers exactly what it answered before, and the
   addition is source-compatible for the call sites that pass a node only.

3. **The validator attributes at the point it already knows the answer.** A port
   condition targets that port; a parameter condition targets that parameter; a
   condition the wire itself causes — a pairing the two port types cannot make, a
   connection naming a node the document does not contain, a node connected to
   itself, a candidate wire that would close a cycle — targets the connection.
   Fan-in is counted per target port, so the port that refuses the second wire is
   the one the condition belongs to.

4. **A condition about the node as a whole keeps no target.** A missing or
   unmigratable definition, and a cycle in whole-document validation, stay
   node-scoped, because neither belongs to one member or one wire. A cycle spans a
   path of several connections, so naming one of them would be arbitrary; the
   candidate wire that closes a cycle is a different case and is attributed to
   that wire.

5. **The projection indexes by the identifiers the target names** and answers
   `DiagnosticsForPort`, `SeverityOfPort`, `DiagnosticsForParameter`,
   `SeverityOfParameter`, `DiagnosticsForConnection`, and `SeverityOfConnection`.

6. **A narrower query reports the broader conditions that make its answer
   unknowable.** A port and a parameter answer with their own conditions preceded
   by the conditions the node reports about itself, so a node-level failure does
   not leave its members looking clean. Document-level conditions are not
   inherited: a graph-wide failure is not a property of one port, and inheriting
   it would mark every connector in the document. A connection inherits neither
   endpoint's conditions, because it belongs to both nodes rather than to one.

7. **Nothing is lost to attribution.** A condition whose target this build cannot
   index — no target, an identifier that cannot be used as a key, or a target kind
   a later contract version adds — is reported at the node scope it names, and a
   condition that names no node stays a document diagnostic. A narrower query for
   an element the projection never saw answers with the nearest broader scope it
   knows and never throws, so a view that outlived the edit which removed its
   element shows the node's state instead of a badge describing something it can
   no longer name.

8. **No WPF, Nodify, or presentation type enters the contract**, so attribution is
   covered by tests that never open a window.

## Consequences

The canvas can mark a rejected wire from its connection identifier and a port can
show its own state, which is what lets a bad parameter stop marking every
connector of its node. Four costs are accepted. The projection holds three more
indexes and one more node-scoped grouping, which is more memory per validation run
in exchange for answering a marker query without re-validating. A port or
parameter condition repeats the node identifier in `NodeInstanceId` and in its
target; the validator constructs both together at every site, and a test holds
that they agree, because a consumer that groups by node must not see a different
owner from a consumer that reads the target. `SelectionValidation` and `Select` are
deliberately unchanged, so narrowing attribution did not narrow what a selection
reports. And a target carries no display text, so a diagnostics panel still reads
the message to describe the condition and the target only to find the element.

## Alternatives considered

Replacing `NodeInstanceId` with a target was rejected: it would change every
grouping consumer at once, and a document-level condition would still need a
representation, so the optional refinement is what keeps the change additive. A
single target record with optional members — a node identifier, a port
identifier, a parameter name, a connection identifier — was rejected because it
makes a port target without a port, or a parameter target without a name,
representable; three sealed records make those states unconstructible. Passing the
document to the projection so it could compare a target against what the document
still contains was rejected because it duplicates the definition resolution the
validator already performed, and because the validator only ever attributes an
element it resolved, so a stale target is not a state it can produce. Deriving the
element in the presentation layer by parsing the message was rejected: it couples
a view model to the wording of a message that must stay translatable.

## Standards impact

Implements the layer rules linked from
[the standards reference](../standards-reference.md): the target hierarchy lives
in `Contracts`, which continues to hold no WPF, Nodify, or presentation type, and
`Application` continues to reference only `Domain` and `Contracts`. No deviation
is required.
