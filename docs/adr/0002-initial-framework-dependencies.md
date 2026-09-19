# ADR-0002 Initial framework dependencies

- **Status:** Accepted
- **Date:** 2026-09-18
- **Decision owners:** VisionWeave maintainers

## Context

The initial skeleton needs a supported WPF shell, node-canvas control, OpenCV binding, MVVM support, and test stack.

## Decision

Use WPF UI 4.3.0 and Nodify 7.3.0 in the App project, OpenCvSharp4 plus OpenCvSharp4.runtime.win 4.13.0.20260627 in the OpenCv project, and CommunityToolkit.Mvvm 8.4.2 in App. The split OpenCV packages avoid pulling WPF conversion concerns into the OpenCv layer. Use the .NET 10 xUnit template package family with Shouldly for tests. Versions are centralized in `Directory.Packages.props`.

WPF UI, Nodify, and CommunityToolkit.Mvvm use MIT licenses. OpenCvSharp uses Apache-2.0. Package restore retains NuGet audit and a source mapping restricted to nuget.org.

## Consequences

The application is Windows-only and targets `net10.0-windows`. Framework-specific types stay outside Contracts and Domain. Dependency upgrades require restore, build, test, and UI smoke verification.

## Alternatives considered

GraphX was rejected because the new design does not carry Aries UI compatibility. A custom canvas was rejected because it would delay product work. A local OpenCvSharp fork was rejected in favor of the maintained upstream package and an integration test for native runtime loading.

## Standards impact

Records the rationale, license posture, target framework, and integration verification for direct architecture-defining dependencies. No deviation from [the adopted standards](../standards-reference.md) is required.
