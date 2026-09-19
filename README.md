# VisionWeave

VisionWeave is a Windows visual image-processing workbench built with .NET 10,
WPF UI, Nodify, and OpenCvSharp. Users compose acquisition, preprocessing,
analysis, inspection, and output steps as a typed workflow graph.

Aries is a reference for discovering historical scenarios and design risks only.
VisionWeave does not promise source, node, execution, file-format, layout, or
side-effect compatibility with Aries.

## Current direction

The product is establishing its WPF host and application boundaries before the
node-editor experience is implemented. The UI will take inspiration from
OpenCMIS's clear application shell and operational feedback, while using an
original dark-amber visual system and a workflow-editor information model.
Read the [pre-UI hardening and visual direction plan](docs/design/pre-ui-hardening-and-visual-direction.md)
before beginning visual UI work.

## Repository layout

- `src/` — product code, separated into Contracts, Domain, Application,
  OpenCV, Persistence, PluginSdk, and the WPF App.
- `tests/` — domain, application, architecture, persistence, integration, and
  app-host tests.
- `docs/` — design records, accepted architecture decisions, quality ledgers,
  and the [documentation map](docs/README.md).
- `scripts/` — repeatable repository quality checks.

## Run locally

```powershell
dotnet run --project src/VisionWeave.App
```

`src/VisionWeave.App/appsettings.json` ships with the executable and provides
defaults for execution concurrency, preview pixel limits, and autosave timing.
An optional `appsettings.user.json` beside it overrides only the settings it
declares. Startup validates every section: unusable configuration terminates
startup with the safe `VW-CONFIG-001` diagnostic instead of silently continuing
with substituted values.

## Verify a change

```powershell
dotnet restore VisionWeave.slnx
dotnet build VisionWeave.slnx --no-restore
dotnet test VisionWeave.slnx --no-build
dotnet format VisionWeave.slnx --verify-no-changes --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ProjectDocuments.ps1
```

## Contributing sequence

Keep `master` releasable. Work one issue at a time: implement a focused,
tested boundary on its issue branch, review whether it is ready to merge, merge
it, then start the next issue. The ordered technical plan is in
[Development plan](docs/design/development-plan.md).
