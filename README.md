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

## Run a packaged build

```powershell
./scripts/Publish-Release.ps1
```

The one command that makes a package. It publishes a self-contained folder for
64-bit Windows to `artifacts/VisionWeave-<version>-win-x64`, where the version is
the one `Directory.Build.props` states and the assemblies in the folder carry it.
Copy the folder anywhere on a Windows 10 or 11 x64 machine and run
`VisionWeave.App.exe`: it holds the .NET runtime, the OpenCV native library, and
the settings file, so the machine needs nothing else installed. It is deliberately
not an installer, not signed, and not a single file
([ADR-0014](docs/adr/0014-packaging.md)).

A published folder can be asked to prove it starts instead of opening a window:

```powershell
$process = Start-Process -FilePath .\VisionWeave.App.exe -ArgumentList '--check' -Wait -PassThru -RedirectStandardOutput check.txt
Get-Content check.txt
$process.ExitCode
```

`--check` composes the host the way startup does, resolves the node catalogue,
and runs one operation through OpenCV's native entry point, writing one line and
exiting instead of opening a window. Exit code 0 means the build starts, 1 means
the settings beside it were rejected, and 2 means the host or the native operation
failed. CI runs the same switch against the folder it published, and uploads that
folder as the `VisionWeave-win-x64` artifact.

## Run from source

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
