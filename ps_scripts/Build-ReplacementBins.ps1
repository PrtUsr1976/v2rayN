param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
$WorkspaceRoot = Split-Path -Parent $RepoRoot
$OutputRoot = Join-Path $WorkspaceRoot 'newbins'
$BuildRoot = Join-Path $RepoRoot 'v2rayN\v2rayN\bin\Release\net10.0-windows10.0.19041.0'
$ProjectPath = Join-Path $RepoRoot 'v2rayN\v2rayN\v2rayN.csproj'

Set-Location -LiteralPath $RepoRoot

if (-not $SkipBuild) {
    Write-Host 'Building v2rayN Release...'
    dotnet build $ProjectPath -c Release -p:EnableWindowsTargeting=true
    if ($LASTEXITCODE -ne 0) {
        throw "v2rayN build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $BuildRoot)) {
    throw "Build output not found: $BuildRoot"
}

$ExpectedOutput = [IO.Path]::GetFullPath((Join-Path $WorkspaceRoot 'newbins'))
$ResolvedWorkspace = [IO.Path]::GetFullPath($WorkspaceRoot)
if (-not $ExpectedOutput.StartsWith(
        $ResolvedWorkspace + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Output path escapes the workspace: $ExpectedOutput"
}

if (Test-Path -LiteralPath $ExpectedOutput) {
    Remove-Item -LiteralPath $ExpectedOutput -Recurse -Force
}
[IO.Directory]::CreateDirectory($ExpectedOutput) | Out-Null

$Files = @(
    'v2rayN.exe',
    'v2rayN.dll',
    'ServiceLib.dll'
)

foreach ($RelativePath in $Files) {
    $Source = Join-Path $BuildRoot $RelativePath
    $Destination = Join-Path $ExpectedOutput $RelativePath
    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Required build file not found: $Source"
    }

    $DestinationDirectory = Split-Path -Parent $Destination
    [IO.Directory]::CreateDirectory($DestinationDirectory) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    Write-Host "Copied $RelativePath"
}

Write-Host "Replacement package is ready: $ExpectedOutput"
