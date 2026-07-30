$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $RepoRoot

if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
    throw "Not a Git repository: $RepoRoot"
}

Write-Host '== repository root =='
git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw 'git rev-parse failed.' }

Write-Host '== remotes =='
git remote -v
if ($LASTEXITCODE -ne 0) { throw 'git remote failed.' }

Write-Host '== branch =='
git branch --show-current
if ($LASTEXITCODE -ne 0) { throw 'git branch failed.' }

Write-Host '== status =='
git status --short --branch
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }

Write-Host '== recent commits =='
git log --oneline --decorate -10
if ($LASTEXITCODE -ne 0) { throw 'git log failed.' }
