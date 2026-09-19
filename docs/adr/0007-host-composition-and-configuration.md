# ADR-0007 Host composition, configuration, and logging stack

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

The editor slice needs a composition root, settings that a deployment can change
without a rebuild, and structured logs, none of which the framework supplies for
a WPF application. ADR-0001 and the detailed design keep `App` as the only
composition root and forbid the core assemblies from knowing how the application
is hosted. ADR-0002 approved the UI, canvas, OpenCV, and MVVM packages but named
no dependency-injection, configuration, or logging stack, so the first slice
that needs one has to record the choice and its boundary.

## Decision

1. **The host stack is `Microsoft.Extensions`.** `DependencyInjection`,
   `Configuration.Json`, `Configuration.Binder`, `Logging`, and
   `Logging.Debug` are referenced by `VisionWeave.App` alone. No core assembly
   references them, which an architecture test now enforces. A settings value
   bound from a file is a typed options record, so the Options abstractions are
   not part of the stack: each layer exposes its record and validates it, and the
   host binds and checks it.

2. **Settings are files beside the executable.** `appsettings.json` ships safe
   defaults and `appsettings.user.json` optionally overrides them on one machine.
   A key the file omits keeps the default its own options record declares, so a
   partial file is a valid file.

3. **Each section is owned by the layer that honors it.** Execution limits belong
   to `ExecutionOptions` in `Application`, preview limits to
   `FramePreviewOptions` in `OpenCv`, and the autosave policy to
   `WorkflowAutosaveOptions` in `Persistence`, which owns the working copy the
   interval refreshes. The host decides which file supplies the values and when
   they are refused, not what the values mean.

4. **A rejected setting stops startup.** Every section is checked before any
   service is built into the shell, and a value outside the range the runtime can
   honor is reported as `VW-CONFIG-001` in a modal message and in the log rather
   than clamped or replaced. Silently running with a limit other than the one
   written down makes a misconfiguration indistinguishable from a defect.

5. **Node definitions enter the catalog through one registration path.** The
   host registers `INodeDefinitionProvider` implementations and builds the
   catalog from all of them, so a duplicate node type identifier fails
   composition instead of shadowing a definition that arrived first.

6. **Logs carry named fields, not prose.** Startup logs the resolved settings and
   the empty document it opened with named placeholders, and a rejected setting
   is logged once at the boundary that reports it.

## Consequences

The host is the only place that knows which implementations ship, so the core
stays testable without a window and the settings validation is unit-tested in the
layers that own it. Two costs are accepted and recorded rather than hidden: the
`App` project has no automated test project, so the wiring itself is verified by
starting the application with a valid and with a rejected settings file, and the
package family is upgraded as a unit together with its central versions.
Structured logging at the execution boundary, the asynchronous command and error
boundary of the shell, and the theme and localization foundations remain to be
built on this stack.

## Alternatives considered

Reusing the `CommunityToolkit.Mvvm` `Ioc` container was rejected because it
provides no typed configuration or logging, so the standards' Options and
structured-logging rules would still need a hand-written substitute, and a
service-locator singleton hides object lifetime. A separate headless
composition project was rejected for now because the design keeps `App` as the
composition root and the wiring is thin; it becomes the better answer if host
wiring ever needs automated coverage that starting the application cannot give.
`Microsoft.Extensions.Hosting` was rejected because a generic host brings
lifetime, environment, and background-service semantics this desktop shell does
not use. Hand-written settings binding and validation was rejected as a
reimplementation of behavior the platform already provides and tests.

## Standards impact

Implements the package-selection, configuration, and observability rules linked
from [the standards reference](../standards-reference.md): central package
versions, selection rationale for boundary-defining packages, typed options with
validated ranges, and structured named log fields. No deviation is required.
