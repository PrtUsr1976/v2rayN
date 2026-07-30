# Codex instructions for this repository

## Repository scope

- Repository: `PrtUsr1976/v2rayN`
- Default branch: `master`
- This is a public, standalone repository based on upstream `2dust/v2rayN`.
- Work from the latest `master` unless the user explicitly requests another branch.
- Do not treat `backup-before-workflow-experiments-20260720` as the working branch.

## Initial Codex orientation

When this repository is first opened in Codex, begin with a read-only orientation pass.

1. Read this `AGENTS.md` completely.
2. Read `README.md` and `docs/TUN_STARTUP_FIX.md`.
3. Inspect the current `master` branch and recent Git history.
4. Locate and understand the existing custom code for subscription headers, `agent_v`, delayed TUN startup, TUN retry/fallback handling, and Windows build variants.
5. Inspect the relevant project structure, tests, and manual GitHub Actions workflows.
6. Do not edit files, create a branch, commit, push, or start a long build during this orientation pass unless the user explicitly requests it.
7. After inspection, give a concise summary of the current project state, note any uncertainties, and state that the repository is ready for the next task.

Do not invent a next task or make preventive changes. Wait for the user's concrete instruction.

## Project purpose

This repository contains a customized Windows build of v2rayN. The main custom features are:

1. Custom HTTP headers for subscription requests loaded from the `agent_v` file.
2. Diagnostic logging of subscription request headers.
3. More resilient Windows TUN startup handling.
4. Delayed TUN activation through `-tundelay <seconds>`.
5. Manual-only GitHub Actions workflows.
6. Light, Medium, and Full Windows x64 build variants.

The repository is not the official upstream v2rayN repository.

## Existing custom behavior

### Subscription headers

The application reads subscription request settings from `agent_v`.

Supported keys include:

```ini
user_agent=Throne/1.1.6
x_hwid=00000000-0000-0000-0000-000000000000
x_device_os=Windows
x_ver_os=10.0.17763
x_device_model=VirtualBox
```

Mappings:

- `user_agent` -> `User-Agent`
- `x_hwid` -> `x-hwid`
- `x_device_os` -> `x-device-os`
- `x_ver_os` -> `x-ver-os`
- `x_device_model` -> `x-device-model`

The code also logs the effective subscription request headers. Do not log passwords, authorization values, tokens, or other secrets in plain text.

Relevant implementation areas include the subscription update service, download helpers, and `AgentVSubscriptionService`.

### TUN startup changes

The custom Windows startup flow supports:

```text
v2rayN.exe -tundelay <seconds>
```

Behavior:

- the presence of `-tundelay` suppresses TUN during initial application startup;
- a positive value enables TUN after the specified delay;
- `-tundelay 0` leaves TUN disabled until enabled manually;
- invalid or missing values are logged and leave TUN disabled;
- both `-tundelay 30` and `-tundelay=30` syntax are supported.

For Windows TUN sessions using Xray or sing-box, the custom logic:

- observes the newly started core process;
- retries startup up to three times if the core exits during observation;
- waits five seconds between attempts;
- cancels the pending sequence when TUN is manually disabled;
- disables TUN and starts the selected server without TUN if all attempts fail.

The observation period is configured in `finetunes.ini`:

```ini
TunStartObservationSeconds=20
```

Accepted values are 20 through 300 seconds. Missing or invalid values are normalized to 20 seconds.

Do not claim that every possible Windows, Wintun, Xray, sing-box, driver, network, or security-software TUN problem has been fixed. Use wording such as "improves startup reliability" or "addresses specific startup failures".

Relevant implementation areas include `StartupArgumentHelper`, `TunStartupSettings`, the Windows application startup code, `CoreManager`, and `ProcessService`.

## Build variants

Custom Windows x64 workflows are manual-only.

- Light: application and libraries, no bundled .NET, no proxy cores.
- Medium: bundled .NET, no proxy cores.
- Full: bundled .NET and proxy cores.

All custom Windows build variants include `agent_v` next to `v2rayN.exe`.

Do not add automatic `push`, `pull_request`, `release`, or scheduled triggers unless the user explicitly requests them.

## Documentation

The main public documentation is stored in:

- `README.md`
- `docs/TUN_STARTUP_FIX.md`

Git history is the authoritative record of completed changes. Important commits include:

- `81a861ec70153766f661ad36c77d73b5ae05229b` - agent_v subscription headers and logging
- `0831e79ab25fa0d968831fcd2e47b5313a9186d6` - delayed and resilient TUN startup
- `912463917e6c81ae78090fd71574a1ff464f0409` - workflows changed to manual-only
- `349f7807a18ddeb14878f1862bfd91b1e64c7c38` - TUN and custom-build documentation

Before changing behavior, inspect the current code and relevant commits instead of relying only on this summary.

## PowerShell scripts

For command-line work on Windows, create and reuse PowerShell scripts under:

```text
ps_scripts/
```

Rules:

1. Put reusable or multi-step PowerShell commands in `.ps1` files inside `ps_scripts` instead of repeatedly sending long inline PowerShell commands.
2. Prefer one script that performs a complete coherent task, so fewer command-execution permission prompts are needed.
3. Reuse and update an existing script when it already covers the task.
4. Make scripts idempotent where practical: rerunning them should not corrupt the repository or duplicate changes.
5. Resolve paths relative to the repository root; do not hard-code the user's local checkout path.
6. Print clear progress and error messages.
7. Stop on errors unless a specific failure is expected and handled.
8. Do not place passwords, tokens, signing secrets, private subscription data, or real HWID values in scripts or commits.
9. Do not require administrator privileges unless the task genuinely needs them. State explicitly when elevation is required.
10. Do not create temporary PowerShell scripts outside `ps_scripts` unless a tool requires it.
11. Keep scripts that are useful for future maintenance. Delete one-off diagnostic scripts only when they have no continuing value.
12. Before running a script that changes files, review its scope and show the resulting diff afterward.

Recommended script structure:

```powershell
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

Write-Host 'Starting task...'

# Task steps

Write-Host 'Task completed.'
```

## Editing rules

- Inspect the current file before editing it.
- Keep changes minimal and scoped to the user's request.
- Do not modify unrelated source code, workflows, releases, tags, or branches.
- Preserve upstream license and attribution.
- Do not overwrite user-specific `agent_v` values with examples.
- Do not commit real identifiers, HWIDs, credentials, or private subscription URLs.
- Do not rewrite large files only to change formatting.
- After changes, inspect `git diff` and verify that only intended files changed.
- Run relevant tests when code changes are made.
- For documentation-only changes, verify links, filenames, command syntax, and statements against current code.

## Testing guidance

For ServiceLib changes, run the relevant test project from the repository root or through a script in `ps_scripts`:

```powershell
dotnet test ./v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj -c Release
```

For Windows application changes, also build the affected project when practical.

Do not report tests as passed unless they were actually executed successfully.

## Current restrictions

Unless the user explicitly requests otherwise:

- do not change source code merely to improve documentation;
- do not modify GitHub Actions triggers;
- do not create a new release;
- do not delete or replace release assets;
- do not delete tags or the backup branch;
- do not merge upstream changes automatically;
- do not claim complete elimination of all TUN hangs.
