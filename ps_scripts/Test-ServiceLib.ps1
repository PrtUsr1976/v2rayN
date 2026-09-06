param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $RepoRoot

$TestProject = Join-Path $RepoRoot 'v2rayN\ServiceLib.Tests\ServiceLib.Tests.csproj'
if (-not (Test-Path -LiteralPath $TestProject)) {
    throw "Test project not found: $TestProject"
}

dotnet test $TestProject -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "ServiceLib tests failed with exit code $LASTEXITCODE."
}
