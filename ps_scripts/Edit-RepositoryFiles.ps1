param(
    [string]$OperationsBase64 = '',
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$RepoPrefix = $RepoRoot + [IO.Path]::DirectorySeparatorChar
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

function Resolve-RepositoryPath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Only non-empty repository-relative paths are allowed: $RelativePath"
    }

    $FullPath = [IO.Path]::GetFullPath((Join-Path $RepoRoot $RelativePath))
    if (-not $FullPath.StartsWith($RepoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes repository root: $RelativePath"
    }
    return $FullPath
}

try {
    if ([string]::IsNullOrWhiteSpace($OperationsBase64)) {
        $OperationsPath = Join-Path $PSScriptRoot 'edit_operations.json'
        $Json = [IO.File]::ReadAllText($OperationsPath)
    } else {
        $Json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($OperationsBase64))
    }
    $ParsedOperations = $Json | ConvertFrom-Json
    $Operations = if ($ParsedOperations -is [Array]) { $ParsedOperations } else { @($ParsedOperations) }
}
catch {
    throw "Edit operations must contain valid UTF-8 JSON: $($_.Exception.Message)"
}
if ($Operations.Count -eq 0) {
    throw 'At least one edit operation is required.'
}

$Prepared = [ordered]@{}
foreach ($Operation in $Operations) {
    $RelativePath = [string]$Operation.path
    $FullPath = Resolve-RepositoryPath $RelativePath
    if (-not [IO.File]::Exists($FullPath)) {
        throw "File does not exist: $RelativePath"
    }

    $Find = [string]$Operation.find
    $Replace = [string]$Operation.replace
    if ([string]::IsNullOrEmpty($Find)) {
        throw "Find text must not be empty: $RelativePath"
    }

    $ExpectedCount = if ($null -eq $Operation.count) { 1 } else { [int]$Operation.count }
    if ($ExpectedCount -lt 1) {
        throw "Expected count must be positive: $RelativePath"
    }

    $Text = if ($Prepared.Contains($FullPath)) {
        $Prepared[$FullPath].UpdatedText
    } else {
        [IO.File]::ReadAllText($FullPath)
    }
    $ActualCount = ([regex]::Matches($Text, [regex]::Escape($Find))).Count
    if ($ActualCount -ne $ExpectedCount) {
        throw "Expected $ExpectedCount matches in $RelativePath, found $ActualCount."
    }

    $Prepared[$FullPath] = [pscustomobject]@{
        RelativePath = $RelativePath
        FullPath = $FullPath
        UpdatedText = $Text.Replace($Find, $Replace)
    }
}

foreach ($Edit in $Prepared.Values) {
    if ($CheckOnly) {
        Write-Host "Check OK: $($Edit.RelativePath)"
        continue
    }

    [IO.File]::WriteAllText($Edit.FullPath, $Edit.UpdatedText, $Utf8NoBom)
    Write-Host "Updated: $($Edit.RelativePath)"
}
