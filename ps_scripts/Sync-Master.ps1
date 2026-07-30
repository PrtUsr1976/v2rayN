param([switch]$FetchOnly)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $RepoRoot

if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
    throw "Not a Git repository: $RepoRoot"
}

$RemoteUrl = git remote get-url origin
if ($LASTEXITCODE -ne 0 -or $RemoteUrl -ne 'https://github.com/PrtUsr1976/v2rayN.git') {
    throw "Unexpected origin: $RemoteUrl"
}

$Pending = git status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
if ($Pending) { throw 'Working tree is not clean; refusing to switch or pull.' }

git fetch origin master
if ($LASTEXITCODE -ne 0) { throw 'git fetch failed.' }

if ($FetchOnly) {
    git status --short --branch
    exit 0
}

git switch master
if ($LASTEXITCODE -ne 0) { throw 'git switch master failed.' }
git pull --ff-only origin master
if ($LASTEXITCODE -ne 0) { throw 'git pull --ff-only failed.' }
git status --short --branch
