<#
.SYNOPSIS
Produces the self-contained Windows folder a user runs.

.DESCRIPTION
The one command that makes a package. It reads what a package is from
Directory.Build.props — the project to publish, the configuration, the runtime
identifier, whether the runtime travels inside, and the output root — publishes
the entry project, and leaves the result in a folder named after the version the
assemblies carry. Every path is resolved from this script's own location, so it
runs from any directory and needs no shell open in the repository.

The published folder is self-contained: it holds the .NET runtime and the native
OpenCV library, so the machine that runs it needs nothing installed. It is not an
installer, not signed, and not a single file (ADR-0014).

.PARAMETER Plan
Prints what the script would publish as JSON and exits without building. The
packaging test reads this, so the settings the repository declares and the publish
the script performs cannot drift apart.

.PARAMETER RepositoryRoot
The repository to publish from. Defaults to the folder that contains this script.

.EXAMPLE
pwsh scripts/Publish-Release.ps1

.EXAMPLE
pwsh scripts/Publish-Release.ps1 -Plan
#>
[CmdletBinding()]
param(
    [switch]$Plan,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw "The repository root does not exist: $RepositoryRoot"
}

$settingsPath = Join-Path $RepositoryRoot 'Directory.Build.props'

if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
    throw "The publish settings are missing: $settingsPath"
}

$settings = New-Object System.Xml.XmlDocument
$settings.Load($settingsPath)

function Read-Setting {
    param([string]$Name)

    $node = $settings.SelectSingleNode("//$Name")

    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "Directory.Build.props does not declare <$Name>, which the publish reads."
    }

    return $node.InnerText.Trim()
}

$version = Read-Setting 'Version'
$project = Read-Setting 'VisionWeavePublishProject'
$configuration = Read-Setting 'VisionWeavePublishConfiguration'
$runtimeIdentifier = Read-Setting 'VisionWeavePublishRuntimeIdentifier'
$selfContained = Read-Setting 'VisionWeavePublishSelfContained'
$outputRoot = Read-Setting 'VisionWeavePublishOutputRoot'

if ($selfContained -ne 'true' -and $selfContained -ne 'false') {
    throw "The declared value of <VisionWeavePublishSelfContained> must be 'true' or 'false', but it is '$selfContained'."
}

$projectPath = Join-Path $RepositoryRoot $project

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "The declared publish project does not exist: $projectPath"
}

# The folder states which version and which runtime it holds, so an artifact
# downloaded from CI is identifiable without opening it.
$folderName = "VisionWeave-$version-$runtimeIdentifier"
$outputPath = Join-Path (Join-Path $RepositoryRoot $outputRoot) $folderName
$outputDirectory = "$outputRoot/$folderName"

$arguments = @(
    'publish',
    $projectPath,
    '--configuration', $configuration,
    '--runtime', $runtimeIdentifier,
    '--self-contained', $selfContained,
    '--output', $outputPath,
    '--nologo'
)

if ($Plan) {
    [ordered]@{
        version                = $version
        project                = $project
        configuration          = $configuration
        runtimeIdentifier      = $runtimeIdentifier
        selfContained          = [System.Convert]::ToBoolean($selfContained)
        outputRoot             = $outputRoot
        outputDirectory        = $outputDirectory
        outputDirectoryAbsolute = $outputPath
        command                = "dotnet publish `"$projectPath`" --configuration $configuration --runtime $runtimeIdentifier --self-contained $selfContained --output `"$outputPath`" --nologo"
    } | ConvertTo-Json

    exit 0
}

# A stale file left beside a fresh one would make the folder something other than
# the publish that just ran, so the folder is replaced rather than merged into.
if (Test-Path -LiteralPath $outputPath) {
    if (-not $folderName.StartsWith('VisionWeave-', [System.StringComparison]::Ordinal)) {
        throw "Refusing to replace '$outputPath': it is not a VisionWeave package folder."
    }

    Write-Host "Replacing the existing folder $outputDirectory"
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

Write-Host "Publishing VisionWeave $version for $runtimeIdentifier to $outputDirectory"

& dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# The publish is not trusted to have produced a runnable folder: the entry
# assembly, the settings file beside it, and the native OpenCV library are what
# `VisionWeave.App.exe --check` needs, and a publish that produced none of them
# should fail here rather than when a user opens the folder.
$required = @(
    (Join-Path $outputPath 'VisionWeave.App.exe'),
    (Join-Path $outputPath 'appsettings.json')
)

foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The published folder does not hold $([System.IO.Path]::GetFileName($path))."
    }
}

$nativeLibrary = Get-ChildItem -LiteralPath $outputPath -Recurse -Filter 'OpenCvSharpExtern.dll' -File |
    Select-Object -First 1

if ($null -eq $nativeLibrary) {
    throw "The published folder does not hold OpenCvSharpExtern.dll, so OpenCV's native entry point did not travel with it."
}

$fileCount = (Get-ChildItem -LiteralPath $outputPath -Recurse -File).Count

Write-Host "Published $fileCount files, including $($nativeLibrary.FullName.Substring($outputPath.Length + 1))."
Write-Host "Ask the published build to prove it starts: $outputDirectory\VisionWeave.App.exe --check"
