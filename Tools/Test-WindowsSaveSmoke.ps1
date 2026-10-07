param(
    [string]$PlayerPath,
    [ValidateRange(5, 60)][int]$TimeoutSeconds = 45,
    [switch]$AllowDirty
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $PlayerPath) { $PlayerPath = Join-Path $repo 'Artifacts\WindowsDevelopment\FallOfCavalry.exe' }
$PlayerPath = (Resolve-Path -LiteralPath $PlayerPath).Path
$sha = git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve start SHA.' }
$initialDirty = @(git -C $repo status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect initial worktree.' }
if ($initialDirty.Count -and -not $AllowDirty) { throw 'Final validation requires a clean worktree; use -AllowDirty only for development diagnostics.' }

# Do not run an older binary whose smoke mode still writes to player saves.
$bootstrapAssembly = Join-Path (Split-Path -Parent $PlayerPath) 'FallOfCavalry_Data\Managed\FOC.Bootstrap.Unity.dll'
$bootstrapSource = Join-Path $repo 'UnityProject\Assets\FOC\Bootstrap\Runtime\DevelopmentCampaignBootstrap.cs'
if ((Get-Item -LiteralPath $bootstrapAssembly).LastWriteTimeUtc -lt (Get-Item -LiteralPath $bootstrapSource).LastWriteTimeUtc) { throw 'Stale bootstrap binary. Rebuild the Windows Development player first.' }
$bootstrapBytes = [IO.File]::ReadAllBytes($bootstrapAssembly)
$marker = 'SMOKE_ROUND_TRIP_MISMATCH'
if (-not ([Text.Encoding]::Unicode.GetString($bootstrapBytes).Contains($marker) -or [Text.Encoding]::Unicode.GetString($bootstrapBytes, 1, $bootstrapBytes.Length - 1).Contains($marker))) { throw 'Bootstrap binary lacks the guarded smoke implementation; refusing to launch it.' }

$settings = Get-Content -LiteralPath (Join-Path $repo 'UnityProject\ProjectSettings\ProjectSettings.asset') -Raw
$company = [regex]::Match($settings, '(?m)^\s*companyName:\s*(.+)$').Groups[1].Value.Trim().Trim('"')
$product = [regex]::Match($settings, '(?m)^\s*productName:\s*(.+)$').Groups[1].Value.Trim().Trim('"')
if (-not $company -or -not $product -or $company.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $product.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Cannot safely determine the player save directory.' }
$appData = Split-Path -Parent ([Environment]::GetFolderPath('LocalApplicationData'))
$playerSaveRoot = Join-Path $appData "LocalLow\$company\$product\FOC\VerticalSliceSaves"
function Get-PlayerSaveSnapshot {
    $items = @()
    if (Test-Path -LiteralPath $playerSaveRoot) {
        $items = @(Get-ChildItem -LiteralPath $playerSaveRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
            [ordered]@{path=$_.FullName;length=$_.Length;modifiedUtc=$_.LastWriteTimeUtc.ToString('o');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
        })
    }
    return ConvertTo-Json -InputObject $items -Depth 4 -Compress
}
$before = Get-PlayerSaveSnapshot
$runId = [Guid]::NewGuid().ToString('N')
$evidence = Join-Path $repo "TestResults\WindowsSaveSmoke\$sha\$runId"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
$failure = $null
$started = [DateTime]::UtcNow
try {
    $cases = @(
        @{name='roundtrip';expectedExit=0;extra=@();marker='FOC_DEVELOPMENT_SMOKE_PASS'},
        @{name='invalid-slot';expectedExit=1;extra=@('-focSmokeSlot','../outside');marker='FOC_DEVELOPMENT_SMOKE_FAIL SAVE_STEP_FAILED'},
        @{name='corrupt-load';expectedExit=1;extra=@('-focSmokeCorruptAfterSave');marker='FOC_DEVELOPMENT_SMOKE_FAIL LOAD_STEP_FAILED'}
    )
    foreach ($case in $cases) {
        $root = Join-Path ([IO.Path]::GetTempPath()) ('foc-windows-smoke\' + [Guid]::NewGuid().ToString('N'))
        $log = Join-Path $evidence ($case.name + '.log')
        $arguments = @('-batchmode','-nographics','-focSmokeTest','-focSmokeSaveRoot',('"'+$root+'"'),'-logFile',('"'+$log+'"')) + $case.extra
        $process = Start-Process -FilePath $PlayerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
        try {
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { Stop-Process -Id $process.Id; throw "Owned smoke process timed out: $($case.name)" }
            $process.Refresh()
            $code = $process.ExitCode
        } finally { $process.Dispose() }
        $text = Get-Content -LiteralPath $log -Raw
        $pass = $code -eq $case.expectedExit -and $text.Contains($case.marker) -and $text.Contains('FOC_DEVELOPMENT_BOOTSTRAP_READY') -and $text.Contains('saveRoot=' + $root)
        if ($case.expectedExit -ne 0) { $pass = $pass -and -not $text.Contains('FOC_DEVELOPMENT_SMOKE_PASS') }
        if ($case.name -eq 'roundtrip') {
            $pass = $pass -and $text.Contains('FOC_SAVE_UI SAVED') -and $text.Contains('FOC_SAVE_UI LOADED') -and -not $text.Contains('FOC_DEVELOPMENT_SMOKE_FAIL')
            $payload = Get-Content -LiteralPath (Join-Path $root 'smoke.focsave') -Raw
            $pass = $pass -and $payload.Contains('SaveVersion=14') -and $text -match 'fingerprint=[0-9a-f]{64}'
        }
        if ($case.name -eq 'invalid-slot') { $pass = $pass -and -not (Test-Path -LiteralPath $root) -and -not $text.Contains('FOC_SAVE_UI LOAD') }
        if ($case.name -eq 'corrupt-load') { $pass = $pass -and $text.Contains('FOC_SMOKE_CORRUPTION_INJECTED') -and $text.Contains('FOC_SAVE_UI LOAD FAILED') }
        $checks.Add([ordered]@{name=$case.name;pass=$pass;exitCode=$code;expectedExit=$case.expectedExit;saveRoot=$root;log=$log;command=($PlayerPath+' '+($arguments -join ' '))})
        Write-Output "FOC_WINDOWS_SAVE_SMOKE $($case.name) pass=$pass exit=$code expected=$($case.expectedExit) sha=$sha"
        if (-not $pass) { throw "Windows smoke evidence failed: $($case.name)" }
    }
} catch { $failure = $_.Exception.Message }
$after = Get-PlayerSaveSnapshot
$endSha = git -C $repo rev-parse HEAD
$gitShaExit = $LASTEXITCODE
$endDirty = @(git -C $repo status --porcelain)
$gitStatusExit = $LASTEXITCODE
$unchanged = $before -ceq $after
if (-not $unchanged) { $failure = 'Player save snapshot changed; no repair or overwrite was attempted.' }
if ($gitShaExit -ne 0 -or $gitStatusExit -ne 0 -or $endSha -ne $sha) { $failure = 'HEAD changed or git evidence became unavailable.' }
if ($endDirty.Count -and -not $AllowDirty) { $failure = 'Final validation dirtied the worktree.' }
if ($checks.Count -ne 3 -and -not $failure) { $failure = 'Not every smoke case ran.' }
$status = if ($failure) { 'FAIL' } elseif ($initialDirty.Count -or $endDirty.Count) { 'DEVELOPMENT_PASS' } else { 'PASS' }
$manifest = Join-Path $evidence 'manifest.json'
[ordered]@{status=$status;runId=$runId;startSha=$sha;endSha=$endSha;startedUtc=$started.ToString('o');completedUtc=[DateTime]::UtcNow.ToString('o');checks=@($checks.ToArray());playerSaveRoot=$playerSaveRoot;playerSavesUnchanged=$unchanged;playerSaveSnapshotBefore=($before | ConvertFrom-Json);playerSaveSnapshotAfter=($after | ConvertFrom-Json);worktree=if($endDirty.Count){'dirty'}else{'clean'};error=$failure} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifest -Encoding utf8
Write-Output "FOC_WINDOWS_SAVE_SMOKE_MANIFEST $status $manifest"
if ($failure) { throw $failure }
