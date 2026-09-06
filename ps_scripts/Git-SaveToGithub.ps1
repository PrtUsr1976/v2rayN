$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$RepoPrefix = $RepoRoot + [IO.Path]::DirectorySeparatorChar
$TaskPath = Join-Path $PSScriptRoot 'git_task.json'
Set-Location -LiteralPath $RepoRoot

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
    throw "Not a Git repository: $RepoRoot"
}

if (Test-Path -LiteralPath $TaskPath) {
    $TaskJson = [IO.File]::ReadAllText($TaskPath)
}
else {
    $TaskJson = Read-Host 'Enter one-line JSON task: {"message":"...","paths":["relative/file"]}'
    if ([string]::IsNullOrWhiteSpace($TaskJson)) {
        throw 'Git task JSON is empty.'
    }
    [IO.File]::WriteAllText($TaskPath, $TaskJson, [Text.UTF8Encoding]::new($false))
}

try {
    $Task = $TaskJson | ConvertFrom-Json
}
catch {
    throw "Invalid git_task.json: $($_.Exception.Message)"
}

$Message = [string]$Task.message
if ([string]::IsNullOrWhiteSpace($Message) -or $Message.Contains([Environment]::NewLine)) {
    throw 'Commit message must be a non-empty single line.'
}

$InputPaths = @($Task.paths)
if ($InputPaths.Count -eq 0) {
    throw 'At least one repository-relative path is required.'
}

$BlockedRoots = @(
    'hwid',
    'subs_links',
    'subscription_configs',
    'downloaded_subscriptions',
    'guiConfigs',
    'newbins',
    'examples',
    'ps_scripts/git_task.json',
    'ps_scripts/edit_operations.json'
)

$RelativePaths = [Collections.Generic.List[string]]::new()
foreach ($InputPath in $InputPaths) {
    $PathText = [string]$InputPath
    if ([string]::IsNullOrWhiteSpace($PathText) -or [IO.Path]::IsPathRooted($PathText)) {
        throw "Only non-empty repository-relative paths are allowed: $PathText"
    }

    $FullPath = [IO.Path]::GetFullPath((Join-Path $RepoRoot $PathText))
    if (-not $FullPath.StartsWith($RepoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes repository root: $PathText"
    }

    $RelativePath = [IO.Path]::GetRelativePath($RepoRoot, $FullPath).Replace('\', '/')
    foreach ($BlockedRoot in $BlockedRoots) {
        if ($RelativePath.Equals($BlockedRoot, [StringComparison]::OrdinalIgnoreCase) -or
            $RelativePath.StartsWith($BlockedRoot + '/', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Private or generated path is blocked: $RelativePath"
        }
    }

    if (-not $RelativePaths.Contains($RelativePath)) {
        $RelativePaths.Add($RelativePath)
    }
}

$Branch = (& git branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Branch)) {
    throw 'Cannot save from a detached HEAD.'
}

$Origin = (& git remote get-url origin).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Origin)) {
    throw 'Remote origin is not configured.'
}

$Allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($RelativePath in $RelativePaths) {
    [void]$Allowed.Add($RelativePath)
}

$StagedBefore = @(Invoke-Git @('-c', 'core.quotepath=false', 'diff', '--cached', '--name-only'))
foreach ($StagedPath in $StagedBefore) {
    if (-not $Allowed.Contains([string]$StagedPath)) {
        throw "Unrelated file is already staged: $StagedPath"
    }
}

$Pathspecs = @($RelativePaths | ForEach-Object { ":(literal)$_" })
Write-Host "Staging $($RelativePaths.Count) explicitly listed path(s)..."
Invoke-Git (@('add', '--') + $Pathspecs)

$Staged = @(Invoke-Git @('-c', 'core.quotepath=false', 'diff', '--cached', '--name-only'))
if ($Staged.Count -eq 0) {
    throw 'No staged changes to commit.'
}
foreach ($StagedPath in $Staged) {
    if (-not $Allowed.Contains([string]$StagedPath)) {
        throw "Refusing to commit unrelated staged file: $StagedPath"
    }
}

Invoke-Git @('diff', '--cached', '--check')
Write-Host '== staged changes =='
Invoke-Git @('diff', '--cached', '--stat')

Write-Host '== commit =='
Invoke-Git @('commit', '-m', $Message, '--')

Write-Host "== push origin/$Branch =="
Invoke-Git @('push', 'origin', 'HEAD')

[IO.File]::Delete($TaskPath)
Write-Host '== final status =='
Invoke-Git @('status', '--short', '--branch')
Write-Host 'Git commit and GitHub push completed.'
