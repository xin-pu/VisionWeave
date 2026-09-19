# ADR-0001 Directed project dependencies

- **Status:** Accepted
- **Date:** 2026-09-18
- **Decision owners:** VisionWeave maintainers

## Context

The editor, execution runtime, OpenCV integration, persistence, and plugin surface must evolve without introducing UI or native-library dependencies into stable contracts.

## Decision

Use the project boundaries defined in the detailed design. Contracts depends only on the platform libraries; Domain depends on Contracts; Application depends on Domain and Contracts; OpenCv and PluginSdk depend on Contracts; Persistence depends on Domain and Contracts; App is the composition root. Architecture tests enforce the most stable boundaries.

## Consequences

The solution contains more projects than a single-assembly prototype, but dependency violations become visible during review and testing. The skeleton contains no Aries project reference.

## Alternatives considered

A single WPF project was rejected because it would couple node definitions, workflow state, OpenCV resources, and UI controls. Reusing Aries assemblies was rejected because Aries is reference material rather than a compatibility target.

## Standards impact

Implements the directed-dependency and nullable-boundary requirements referenced by [the adopted standards](../standards-reference.md). No deviation is required.
