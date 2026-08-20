param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Status', 'Search', 'ReadRange', 'RuntimeLog')]
    [string]$Action,

    [string[]]$Path = @(),
    [string[]]$Pattern = @(),
    [int]$Skip = 0,
    [int]$First = 200,
    [int]$Last = 200,
    [string]$RuntimeRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$RepoPrefix = $RepoRoot + [IO.Path]::DirectorySeparatorChar

function Resolve-RepoFile {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    if ([IO.Path]::IsPathRooted($RelativePath)) {
        throw "Repository paths must be relative: $RelativePath"
    }
    $FullPath = [IO.Path]::GetFullPath((Join-Path $RepoRoot $RelativePath))
    if (-not $FullPath.StartsWith($RepoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes repository root: $RelativePath"
    }
    return $FullPath
}

switch ($Action) {
    'Status' {
        git -C $RepoRoot status --short --branch
        git -C $RepoRoot diff --stat
        git -C $RepoRoot diff --cached --stat
    }
    'Search' {
        if ($Pattern.Count -eq 0 -or $Path.Count -eq 0) {
            throw 'Search requires -Pattern and -Path.'
        }
        $ResolvedPaths = @($Path | ForEach-Object { Resolve-RepoFile $_ })
        $Arguments = @('-n', '--no-heading')
        foreach ($Item in $Pattern) {
            $Arguments += @('-e', $Item)
        }
        $Arguments += $ResolvedPaths
        & rg @Arguments
        if ($LASTEXITCODE -gt 1) {
            throw "rg failed with exit code $LASTEXITCODE."
        }
    }
    'ReadRange' {
        if ($Path.Count -ne 1) {
            throw 'ReadRange requires exactly one -Path.'
        }
        $FullPath = Resolve-RepoFile $Path[0]
        Get-Content -LiteralPath $FullPath -Encoding UTF8 |
            Select-Object -Skip $Skip -First $First
    }
    'RuntimeLog' {
        if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
            throw 'RuntimeLog requires -RuntimeRoot.'
        }
        $RuntimeRoot = [IO.Path]::GetFullPath($RuntimeRoot)
        $LogRoot = Join-Path $RuntimeRoot 'guiLogs'
        if (-not [IO.Directory]::Exists($LogRoot)) {
            throw "Runtime log directory does not exist: $LogRoot"
        }
        $LogFile = Get-ChildItem -LiteralPath $LogRoot -File -Filter '*.txt' |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -eq $LogFile) {
            throw "No text logs found in: $LogRoot"
        }
        if ($Pattern.Count -eq 0) {
            Get-Content -LiteralPath $LogFile.FullName -Encoding UTF8 |
                Select-Object -Last $Last
        }
        else {
            Select-String -LiteralPath $LogFile.FullName -Pattern $Pattern |
                Select-Object -Last $Last
        }
    }
}
