param(
    [string]$ReferencePath = '',
    [string]$ActualPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
$WorkspaceRoot = Split-Path -Parent $RepoRoot
if ([string]::IsNullOrWhiteSpace($ReferencePath)) {
    $ReferencePath = Join-Path $WorkspaceRoot 'examples\xkeen_sets'
}
if ([string]::IsNullOrWhiteSpace($ActualPath)) {
    $ActualPath = Join-Path $WorkspaceRoot 'examples\xkeen_sets_new'
}

$ReferencePath = [IO.Path]::GetFullPath($ReferencePath)
$ActualPath = [IO.Path]::GetFullPath($ActualPath)
if (-not (Test-Path -LiteralPath $ReferencePath -PathType Container)) {
    throw "Reference directory not found: $ReferencePath"
}
if (-not (Test-Path -LiteralPath $ActualPath -PathType Container)) {
    throw "Actual directory not found: $ActualPath"
}

function Get-RelativeFiles {
    param([string]$Root)

    return Get-ChildItem -LiteralPath $Root -Recurse -File |
        ForEach-Object { $_.FullName.Substring($Root.Length).TrimStart('\') } |
        Sort-Object
}

function ConvertTo-CanonicalObject {
    param($Value)

    if ($null -eq $Value) {
        return $null
    }
    if ($Value -is [System.Collections.IDictionary]) {
        $result = [ordered]@{}
        foreach ($key in ($Value.Keys | Sort-Object)) {
            $result[$key] = ConvertTo-CanonicalObject $Value[$key]
        }
        return $result
    }
    if ($Value -is [pscustomobject]) {
        $result = [ordered]@{}
        foreach ($property in ($Value.PSObject.Properties | Sort-Object Name)) {
            $result[$property.Name] = ConvertTo-CanonicalObject $property.Value
        }
        return $result
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        return @($Value | ForEach-Object { ConvertTo-CanonicalObject $_ })
    }
    return $Value
}

$referenceFiles = @(Get-RelativeFiles $ReferencePath)
$actualFiles = @(Get-RelativeFiles $ActualPath)
$missingFiles = @($referenceFiles | Where-Object { $_ -notin $actualFiles })
$extraFiles = @($actualFiles | Where-Object { $_ -notin $referenceFiles })
$commonFiles = @($referenceFiles | Where-Object { $_ -in $actualFiles })

$differences = @()
foreach ($relativePath in $commonFiles) {
    $referenceFile = Join-Path $ReferencePath $relativePath
    $actualFile = Join-Path $ActualPath $relativePath
    if ([IO.Path]::GetExtension($relativePath) -ieq '.json') {
        try {
            $referenceObject = Get-Content -LiteralPath $referenceFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $actualObject = Get-Content -LiteralPath $actualFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $referenceText = ConvertTo-CanonicalObject $referenceObject | ConvertTo-Json -Depth 100 -Compress
            $actualText = ConvertTo-CanonicalObject $actualObject | ConvertTo-Json -Depth 100 -Compress
        }
        catch {
            $differences += [pscustomobject]@{
                File = $relativePath
                Kind = 'Invalid JSON'
                Detail = $_.Exception.Message
            }
            continue
        }
    }
    else {
        $referenceText = (Get-Content -LiteralPath $referenceFile -Raw -Encoding UTF8).Replace("`r`n", "`n").TrimEnd()
        $actualText = (Get-Content -LiteralPath $actualFile -Raw -Encoding UTF8).Replace("`r`n", "`n").TrimEnd()
    }

    if ($referenceText -cne $actualText) {
        $differences += [pscustomobject]@{
            File = $relativePath
            Kind = if ([IO.Path]::GetExtension($relativePath) -ieq '.json') { 'JSON content' } else { 'Text content' }
            Detail = ''
        }
    }
}

Write-Host "Reference: $ReferencePath"
Write-Host "Actual:    $ActualPath"
Write-Host "Reference files: $($referenceFiles.Count)"
Write-Host "Actual files:    $($actualFiles.Count)"

Write-Host "`nMissing files:"
$missingFiles | ForEach-Object { Write-Host "  $_" }
Write-Host "`nExtra files:"
$extraFiles | ForEach-Object { Write-Host "  $_" }
Write-Host "`nDifferent common files:"
$differences | Format-Table -AutoSize

Write-Host "`nJSON endpoint summary:"
Get-ChildItem -LiteralPath $ActualPath -Recurse -Filter '04_outbounds.json' |
    Sort-Object FullName |
    ForEach-Object {
        $json = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $proxy = @($json.outbounds)[0]
        $vnext = @($proxy.settings.vnext)[0]
        [pscustomobject]@{
            File = $_.FullName.Substring($ActualPath.Length).TrimStart('\')
            Protocol = $proxy.protocol
            Address = $vnext.address
            Port = $vnext.port
            Network = $proxy.streamSettings.network
            Security = $proxy.streamSettings.security
        }
    } | Format-Table -AutoSize
