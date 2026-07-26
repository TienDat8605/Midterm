<#
.SYNOPSIS
Builds DINO PARK for WebGL, uploads it to itch.io, and displays channel status.

.EXAMPLE
.\Scripts\Deploy-WebToItch.ps1 -Version 1.0.1

.EXAMPLE
.\Scripts\Deploy-WebToItch.ps1
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Version = (Get-Date -Format "yyyy.MM.dd.HHmm"),

    [ValidatePattern("^[^/:\s]+/[^/:\s]+$")]
    [string]$ItchTarget = "noridom0/dino-park",

    [ValidatePattern("^[^:\s]+$")]
    [string]$Channel = "web",

    [ValidateNotNullOrEmpty()]
    [string]$UnityPath = "D:\Unity\Editor\6000.3.16f1\Editor\Unity.exe",

    [ValidateNotNullOrEmpty()]
    [string]$ButlerPath = "butler"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-Executable {
    param(
        [Parameter(Mandatory)]
        [string]$PathOrCommand,

        [Parameter(Mandatory)]
        [string]$DisplayName
    )

    if (Test-Path -LiteralPath $PathOrCommand -PathType Leaf) {
        return (Resolve-Path -LiteralPath $PathOrCommand).Path
    }

    $command = Get-Command -Name $PathOrCommand -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "$DisplayName was not found. Install it or pass its full path with -${DisplayName}Path."
    }

    return $command.Source
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Executable,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Description
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$buildDirectory = Join-Path $projectRoot "Builds\Web\Release"
$zipPath = Join-Path $projectRoot "Builds\Web\DinoPark-Web.zip"
$logDirectory = Join-Path $projectRoot "Builds\Logs"
$logPath = Join-Path $logDirectory "WebGL-build.log"
$editorLock = Join-Path $projectRoot "Temp\UnityLockfile"
$destination = "${ItchTarget}:$Channel"
$targetParts = $ItchTarget.Split("/")
$projectPage = "https://$($targetParts[0]).itch.io/$($targetParts[1])"

$unity = Resolve-Executable -PathOrCommand $UnityPath -DisplayName "Unity"
$butler = Resolve-Executable -PathOrCommand $ButlerPath -DisplayName "Butler"

if (Test-Path -LiteralPath $editorLock -PathType Leaf) {
    throw "This Unity project appears to be open. Close the Unity Editor, then run this script again."
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

Write-Host ""
Write-Host "Building DINO PARK WebGL..." -ForegroundColor Cyan
Write-Host "Scenes are resolved by DinoParkBuildTools; test scenes are excluded."

$unityArguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", $projectRoot,
    "-buildTarget", "WebGL",
    "-executeMethod", "DinoParkBuildTools.BuildWebRelease",
    "-quit",
    "-logFile", $logPath
)

try {
    Invoke-NativeCommand -Executable $unity -Arguments $unityArguments -Description "Unity WebGL build"
}
catch {
    if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Write-Host ""
        Write-Host "Last 80 lines from the Unity build log:" -ForegroundColor Yellow
        Get-Content -LiteralPath $logPath -Tail 80
    }
    throw
}

$indexPath = Join-Path $buildDirectory "index.html"
if (!(Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw "Build validation failed: $indexPath is missing."
}
if (!(Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "Build validation failed: $zipPath is missing."
}

Write-Host ""
Write-Host "Build succeeded. Uploading $destination version $Version..." -ForegroundColor Green
Invoke-NativeCommand -Executable $butler -Arguments @(
    "push",
    $buildDirectory,
    $destination,
    "--userversion", $Version
) -Description "itch.io upload"

Write-Host ""
Write-Host "Current itch.io channel status:" -ForegroundColor Cyan
Invoke-NativeCommand -Executable $butler -Arguments @(
    "status",
    $destination
) -Description "itch.io status check"

Write-Host ""
Write-Host "Deployment completed successfully." -ForegroundColor Green
Write-Host "Project page: $projectPage"
Write-Host "Unity log: $logPath"
