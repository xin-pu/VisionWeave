# ADR-0014 The shape of a package

- **Status:** Accepted
- **Date:** 2026-09-20
- **Decision owners:** VisionWeave maintainers

## Context

Stage 7 of the development plan names packaging, and item 8 of the delivery plan
names it again, but nothing had ever built one: no publish profile, no script, no
artifact job. Every capability the repository had verified could only be used by
someone with the SDK, the source, and a shell open in the right folder, and the
stage's completion gate — a package that starts on a clean Windows host — had no
artifact to check against.

What a package is decides several things at once: what a user downloads, what a
failure looks like when a prerequisite is missing, how much has to be re-verified
when a dependency changes, and which parts of a release process exist at all. This
build is a Windows-only WPF application with one native dependency (OpenCvSharp's
`OpenCvSharpExtern.dll`), no auto-update mechanism, no installer authoring, no
signing certificate, and no release process to hang a package off.

## Decision

1. **A package is a self-contained folder for `win-x64`.** The .NET runtime and
   the native OpenCV library travel inside it, and the machine that runs it needs
   nothing installed. Unpacked, this build is about 241 MB; a framework-dependent
   publish would be a few megabytes and would move the failure onto the machine
   that runs it, as a dialog about a missing runtime whose owner cannot act on the
   document that came with the image. The stage's completion gate asks for a build
   that starts on a clean Windows host, which is the property the extra size buys,
   and it is what makes the folder a package rather than an archive someone still
   has to configure.

2. **One command produces it, and the repository declares what it produces.**
   `scripts/Publish-Release.ps1` publishes the entry project and leaves the result
   in `artifacts/VisionWeave-<version>-<runtime>`. It reads every setting — the
   project, the configuration, the runtime identifier, whether the runtime travels
   inside, and the output root — from `Directory.Build.props` instead of restating
   it, resolves each path from its own location so it runs from any directory, and
   replaces the folder it produced last time rather than merging into it. It also
   refuses to finish if the folder it produced does not hold the entry assembly,
   the settings file beside it, and the native OpenCV library.

3. **The version is stated once for the whole repository.** `Directory.Build.props`
   declares one `<Version>`, which stamps every assembly and names the folder the
   script produces, so a package cannot claim a version the assemblies do not
   carry. No project states a version of its own.

4. **A published build can be asked to prove it starts.** `VisionWeave.App.exe
   --check` composes the host the way startup does, resolves the catalog, runs one
   node's executor through OpenCV's native entry point, writes one line, and exits
   with a code instead of opening a window: 0 when the build starts, 1 when the
   settings beside it were rejected, 2 when the host could not compose or the
   native operation failed. It is the entry point's own path through composition,
   so it fails on a settings file, a catalog contribution, a native library, or a
   frame lease that did not travel with the publish.

5. **CI runs that same command and hands the folder over.** The `package` job runs
   after `verify`, runs the script, asserts the produced folder holds the entry
   assembly, `appsettings.json`, and `OpenCvSharpExtern.dll`, runs `--check`
   against the published folder rather than the build output, asserts the exit code
   and that the version the folder names is the version the build reported, and
   uploads the folder as a workflow artifact.

6. **What a package deliberately is not.** Not an installer: an MSI or an Inno
   Setup script is a decision about install locations, registry entries, upgrades,
   and uninstall behaviour, and nothing here has a reason to write outside the
   folder a user unzips into. Not signed: signing needs a certificate this project
   does not have, and an unsigned installer is worse than an unsigned folder. Not a
   single file: WPF together with the OpenCvSharp native library is a known-bad
   combination for single-file extraction, and a folder that states its own layout
   is easier to inspect when a native library fails to load. Not versioned releases
   or a changelog: tagging and publishing a release is its own workflow over this
   one.

## Consequences

A user downloads one artifact, unzips it anywhere, and runs the executable. There
is nothing to install, nothing to configure, and no prerequisite to explain, and
the same folder runs on a machine that never had the SDK.

The cost is size and a repeated dependency: the folder carries the runtime, so a
runtime update is a new folder rather than a service pack. The recorded size in
PL-2026-024 is the measured one, because a package's own cost is what a maintainer
decides against.

CI can prove the folder was produced and that its own entry point starts and runs
a native operation. It cannot see a window, and a runner is not a clean Windows
host, so the last step of the stage's gate — a build that opens its window on a
machine with no SDK — stays a human check, recorded in PL-2026-024 rather than
implied by a green run.

A version change now moves three things at once, which is the point: the folder
name, the assemblies, and the line the release check prints. A dependency upgrade
is re-verified by running the script and the check, which is also how a broken
native asset is found.

What this decision does not settle: a plugin folder, license and notice files
shipped beside the executable, code signing and its certificate story, an update
mechanism, and the release workflow that would carry the artifact to a download
page. Each is a decision of its own, and none of them is needed for the folder to
run.

## Alternatives considered

**A framework-dependent publish.** A few megabytes instead of about 241, and it
matches how the development machines already run. It was rejected because the
failure it creates is invisible here and immediate there: the user meets a dialog
about a runtime they do not have, and the document that shipped with the folder
cannot tell them what to install. Self-contained makes the missing-prerequisite
failure a build-time problem, which is where this repository can see it.

**An installer (MSI or Inno Setup).** An installer is what a Windows user often
expects, and it would handle a start-menu entry and an uninstall record. It was
rejected because it brings install locations, registry state, upgrades, and
uninstall behaviour into a product that has no reason to write outside its own
folder, and because an unsigned installer asks for elevation and a trust decision
that an unsigned folder does not.

**A single file.** One executable is the smallest thing to hand over, and .NET can
produce it. It was rejected because WPF together with a native library extracts to
a temporary directory at startup, which turns a native load failure into a silent
one, and because the components of a self-contained folder are worth being able to
inspect when a dependency fails to load.

**Signing the build.** A signed artifact carries a publisher identity and is what
an installer would need. It was rejected for now because no certificate exists,
and a signing step without one is either a failure or a placeholder that claims a
verification nobody performed.

**Declaring the publish settings in a `.pubxml` publish profile.** A publish
profile is the idiomatic place for these settings, and MSBuild consumes it
directly. It was rejected because the default .gitignore excludes `*.pubxml`, so
the declaration would either be untracked or need an exception, because a profile
cannot carry the version the folder is named after, and because the packaging test
would have to parse a file written for the Visual Studio publish wizard rather
than the file the version already lives in.

**A release workflow with tags and published releases.** A tagged release would
give the artifact a stable download URL. It was rejected as the next workflow
rather than this one: the folder has to exist and prove it starts before a release
process has anything to hand out.

## Standards impact

No deviation. The command is a script under `scripts/` with the surrounding
scripts' shape, the version lives in the repository's shared build properties
rather than in each project, and the packaging test reads repository files the way
the existing convention tests do.
