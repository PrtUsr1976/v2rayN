param(
    [string]$Pattern = 'agent_v|AgentVSubscriptionService|tundelay|TunStartObservationSeconds|StartTunWithRetry|workflow_dispatch'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $RepoRoot

Write-Host '== source matches =='
rg -n -S --glob '!**/bin/**' --glob '!**/obj/**' $Pattern .
if ($LASTEXITCODE -notin 0, 1) { throw 'rg failed.' }

Write-Host '== matching commit history =='
git log --all --oneline --decorate -G $Pattern -30
if ($LASTEXITCODE -ne 0) { throw 'git log search failed.' }
