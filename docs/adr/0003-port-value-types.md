# ADR-0003 Port value types and connection compatibility

- **Status:** Proposed
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

The detailed design requires typed ports, explicit conversion nodes, and a
`Contracts` assembly that depends on nothing but the platform libraries. A port
value cannot be represented by an OpenCvSharp type such as `Mat`, `Point`, or
`RotatedRect`, because `Contracts` must not reference OpenCvSharp, and it cannot
be represented by a WPF type such as `System.Windows.Rect`, because `Contracts`
must not reference presentation assemblies. A persisted document must also be
readable when the defining plugin is absent, so a port type cannot be identified
by a CLR `Type` or by `Type.FullName` alone.

## Decision

1. **Identity is a stable identifier, not a CLR type.** Every port type is
   identified by `PortTypeId`, a provider-qualified string such as
   `visionweave.type.image-frame`. Built-in identifiers are declared once in
   `VisionWeave.Contracts` as a static identifier type and are referenced by
   production code, persistence, and tests.

2. **Each port type has exactly one representation declared in `Contracts`,
   using platform-neutral types only.**

   | Port type id | Contract representation | Notes |
   | --- | --- | --- |
   | `visionweave.type.image-frame` | `ImageFrameLease` | Read-only, reference-counted lease. Never exposes a mutable `Mat`. |
   | `visionweave.type.contour-collection` | `ContourCollection` of `Contour` | `Contour` is a read-only point sequence. |
   | `visionweave.type.rectangle` | `Rectangle` | `System.Drawing.Primitives`. |
   | `visionweave.type.rectangle-collection` | `RectangleCollection` | Ordered, read-only. |
   | `visionweave.type.point-collection` | `PointCollection` | `System.Drawing.Primitives.Point`. |
   | `visionweave.type.rotated-rectangle-collection` | `RotatedRectangleCollection` | `RotatedRectangle` carries center, size, and angle. |
   | `visionweave.type.scalar` | `ScalarValue` | Up to four components; used for colors and thresholds. |
   | `visionweave.type.number` | `double` | |
   | `visionweave.type.boolean` | `bool` | |
   | `visionweave.type.text` | `string` | |

3. **Port values are carried in a closed value hierarchy.** `PortValue` is an
   abstract record in `Contracts` with one derived record per port type, each
   exposing its `PortTypeId`. A closed hierarchy keeps exhaustiveness checks
   possible and prevents plugins from inventing values that persistence, the
   scheduler, and preview rendering cannot interpret.

4. **Compatibility is an explicit, declared relation.** `PortTypeCompatibility`
   in `Contracts` declares the complete set of accepted assignments. Identical
   port types are always compatible; any other accepted pair must be listed
   explicitly, and every remaining pair is rejected with `VW-PORT-001`. Runtime
   coercion is never implicit, and a conversion that changes meaning is a node.

5. **Multiplicity is a connection-count rule, not a collection value.**
   `PortMultiplicity` is either `Single` (at most one incoming connection) or
   `Many` (fan-in over several connections, collected in connection order).
   Collection *values* are expressed by port types such as
   `visionweave.type.image-frame-collection`, never by multiplicity.

6. **Batch and collection flow is deferred, not implied.** A port type that
   carries several images with per-element execution semantics is out of scope
   for the first release, because per-element execution changes cache keys,
   progress, diagnostics, and preview selection. The Aries `Mats`/`Mat[]` family
   is reference material for that future work, not a first-release contract.

7. **OpenCvSharp appears only in `VisionWeave.OpenCv`.** That project implements
   `ImageFrameLease` over `Mat` and owns the mapping between contract values and
   OpenCvSharp types. `Contracts` never names an OpenCvSharp type, and no
   executor outside the OpenCV project receives one.

## Consequences

Plugins can add port types only by registering a value record and a mapper in
their own assembly; the built-in node catalog uses exactly the same path. The
`PortValue` hierarchy and the compatibility table become the single place where
connection legality is defined, so the canvas, the application validator, and
the scheduler all agree. Adding a port type is a deliberate, reviewed change
rather than an implicit effect of a CLR type appearing somewhere in the graph.

The cost is indirection: every node executor converts contract values to
OpenCvSharp types at its boundary, and a plugin that wants a native `Mat` must
own that conversion.

## Alternatives considered

Representing port types by CLR `Type` was rejected because a document could then
not be loaded without the defining plugin, and because `Type.FullName` was the
mechanism that made the Aries format collapse when a block was missing. Exposing
`Mat` on a normal port was rejected by the native ownership decision in
[ADR-0005](0005-native-resource-ownership.md). Using WPF geometry types was
rejected because it would pull presentation assemblies into `Contracts`.

## Standards impact

Implements the stable-contract-identifier and directed-dependency rules linked
from [the standards reference](../standards-reference.md). No deviation is
required.
