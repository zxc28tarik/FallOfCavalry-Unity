param(
    [string]$PlayerPath,
    [ValidateRange(10,14400)][int]$DurationSeconds=7200,
    [switch]$AllowDirty,
    [switch]$MemoryAudit
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $PlayerPath){$PlayerPath=Join-Path $repo 'Artifacts\WindowsDevelopment\FallOfCavalry.exe'}
$PlayerPath=(Resolve-Path -LiteralPath $PlayerPath).Path
$sha=git -C $repo rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Cannot resolve HEAD'}
$initialDirty=@(git -C $repo status --porcelain)
if($LASTEXITCODE -ne 0){throw 'Cannot inspect worktree'}
if($initialDirty.Count -and -not $AllowDirty){throw 'Final player soak requires a clean worktree; -AllowDirty is diagnostic only.'}
$assembly=Join-Path (Split-Path -Parent $PlayerPath) 'FallOfCavalry_Data\Managed\FOC.Bootstrap.Unity.dll'
foreach($source in @('DevelopmentCampaignBootstrap.cs','DevelopmentPlayerSoak.cs')){
    if((Get-Item -LiteralPath $assembly).LastWriteTimeUtc -lt (Get-Item -LiteralPath (Join-Path $repo ('UnityProject\Assets\FOC\Bootstrap\Runtime\'+$source))).LastWriteTimeUtc){throw 'Stale player: rebuild Windows Development first.'}
}
$bytes=[IO.File]::ReadAllBytes($assembly)
if(-not ([Text.Encoding]::Unicode.GetString($bytes).Contains('FOC_PLAYER_SOAK_READY') -or [Text.Encoding]::Unicode.GetString($bytes,1,$bytes.Length-1).Contains('FOC_PLAYER_SOAK_READY'))){throw 'Player has no isolated soak harness.'}

$settings=Get-Content -LiteralPath (Join-Path $repo 'UnityProject\ProjectSettings\ProjectSettings.asset') -Raw
$company=[regex]::Match($settings,'(?m)^\s*companyName:\s*(.+)$').Groups[1].Value.Trim().Trim('"')
$product=[regex]::Match($settings,'(?m)^\s*productName:\s*(.+)$').Groups[1].Value.Trim().Trim('"')
foreach($name in @($company,$product)){if(-not $name -or $name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){throw 'Cannot safely determine normal player saves.'}}
$appData=Split-Path -Parent ([Environment]::GetFolderPath('LocalApplicationData'))
$playerSaveRoot=Join-Path $appData "LocalLow\$company\$product\FOC\VerticalSliceSaves"
function Snapshot-PlayerSaves {
    $items=@()
    if(Test-Path -LiteralPath $playerSaveRoot){$items=@(Get-ChildItem -LiteralPath $playerSaveRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=$_.FullName;length=$_.Length;modifiedUtc=$_.LastWriteTimeUtc.ToString('o');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })}
    ConvertTo-Json -InputObject $items -Depth 4 -Compress
}
$before=Snapshot-PlayerSaves
$runId=[Guid]::NewGuid().ToString('N')
$evidence=Join-Path $repo "TestResults\WindowsPlayerSoak\$sha\$runId"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$saveRoot=Join-Path ([IO.Path]::GetTempPath()) ('foc-windows-smoke\'+$runId)
$reportPath=Join-Path $saveRoot 'player-soak.json'
$log=Join-Path $evidence 'player.log'
$samplesPath=Join-Path $evidence 'process-samples.csv'
$manifestPath=Join-Path $evidence 'manifest.json'
$arguments=@('-batchmode','-force-d3d11','-screen-fullscreen','0','-screen-width','1366','-screen-height','768','-focPlayerSoak','-focSoakSeconds',$DurationSeconds,'-focSmokeSaveRoot',('"'+$saveRoot+'"'),'-logFile',('"'+$log+'"'))
if($MemoryAudit){$arguments+='-focSoakMemoryAudit'}
$started=[DateTime]::UtcNow
$process=$null;$ownedProcessId=$null;$exitCode=$null;$report=$null;$failure=$null;$samples=0;$workingBaseline=0L;$peakWorking=0L;$lastHeartbeat=0.0;$lastProgress=$started
function Write-Manifest([string]$Status) {
    [ordered]@{status=$Status;runKind=if($MemoryAudit){'MEMORY_DIAGNOSTIC'}elseif($DurationSeconds -lt 3600){'SHORT_FLOW'}else{'LONG_SOAK'};memoryAudit=[bool]$MemoryAudit;startSha=$sha;endSha=$endSha;runId=$runId;startedUtc=$started.ToString('o');updatedUtc=[DateTime]::UtcNow.ToString('o');requestedSeconds=$DurationSeconds;playerPath=$PlayerPath;bootstrapSha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash;command=$PlayerPath+' '+($arguments -join ' ');processId=$ownedProcessId;exitCode=$exitCode;saveRoot=$saveRoot;log=$log;playerReport=$reportPath;processSamples=$samplesPath;sampleCount=$samples;peakWorkingSetBytes=$peakWorking;warmWorkingSetBytes=$workingBaseline;playerSavesUnchanged=$unchanged;playerSaveSnapshotBefore=($before | ConvertFrom-Json);playerSaveSnapshotAfter=if($after){$after | ConvertFrom-Json}else{$null};worktree=$worktree;error=$failure;uiInput='Synthetic UI Toolkit NavigationSubmit/KeyDown events on real attached runtime controls, not OS mouse/keyboard input';rendering='D3D11 requested; no -nographics; no art-quality acceptance'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
}
$endSha=$sha;$worktree=if($initialDirty.Count){'dirty'}else{'clean'};$unchanged=$null;$after=$null
Write-Manifest 'RUNNING'
Write-Output "FOC_WINDOWS_PLAYER_SOAK_START sha=$sha duration=$DurationSeconds manifest=$manifestPath"
try {
    $process=Start-Process -FilePath $PlayerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $ownedProcessId=$process.Id
    Write-Manifest 'RUNNING'
    $csv=[IO.StreamWriter]::new($samplesPath,$false,[Text.UTF8Encoding]::new($false))
    try {
        $csv.WriteLine('utc,elapsed_seconds,working_set_bytes,private_bytes,cpu_seconds,cycles,frames')
        while(-not $process.HasExited){
            Start-Sleep -Seconds 2
            $process.Refresh()
            if($process.HasExited){break}
            $elapsed=([DateTime]::UtcNow-$started).TotalSeconds
            if($elapsed -gt $DurationSeconds+120){throw 'Player exceeded requested duration plus startup/shutdown allowance.'}
            if(Test-Path -LiteralPath $reportPath){
                try{$candidate=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json; $report=$candidate}catch{}
                if($report -and [double]$report.elapsedSeconds -gt $lastHeartbeat){$lastHeartbeat=[double]$report.elapsedSeconds;$lastProgress=[DateTime]::UtcNow}
            }
            if(([DateTime]::UtcNow-$lastProgress).TotalSeconds -gt 90){throw 'No UI-loop heartbeat for 90 seconds; possible stall/startup failure.'}
            $peakWorking=[Math]::Max($peakWorking,$process.WorkingSet64)
            if($elapsed -ge [Math]::Min(60,$DurationSeconds/3) -and $workingBaseline -eq 0){$workingBaseline=$process.WorkingSet64}
            if($workingBaseline -gt 0 -and $process.WorkingSet64-$workingBaseline -gt 256MB){throw 'Working-set growth exceeded the workload-specific 256 MiB guard.'}
            $csv.WriteLine(('{0},{1},{2},{3},{4},{5},{6}' -f [DateTime]::UtcNow.ToString('o'),$elapsed.ToString('F3',[Globalization.CultureInfo]::InvariantCulture),$process.WorkingSet64,$process.PrivateMemorySize64,$process.TotalProcessorTime.TotalSeconds.ToString('F3',[Globalization.CultureInfo]::InvariantCulture),$(if($report){$report.cycles}else{0}),$(if($report){$report.frames}else{0})))
            $csv.Flush();$samples++
            if($samples%30 -eq 0){Write-Manifest 'RUNNING';Write-Output "FOC_WINDOWS_PLAYER_SOAK_PROGRESS elapsed=$([int]$elapsed) cycles=$($report.cycles) frames=$($report.frames) working=$($process.WorkingSet64)"}
        }
        $process.WaitForExit();$process.Refresh();$exitCode=$process.ExitCode
    }finally{$csv.Dispose()}
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $text=Get-Content -LiteralPath $log -Raw
    $expectedStatus=if($MemoryAudit){'DIAGNOSTIC_COMPLETE'}else{'PASS'}
    if($exitCode -ne 0 -or $report.status -ne $expectedStatus -or [bool]$report.memoryAudit -ne [bool]$MemoryAudit -or $report.requestedSeconds -ne $DurationSeconds -or $report.elapsedSeconds -lt $DurationSeconds -or $report.roundtrips -lt 2 -or $report.lifecycleChecks -ne 1 -or $report.graphicsDevice -eq 'Null' -or $samples -lt 2 -or -not $text.Contains('FOC_PLAYER_SOAK_'+$expectedStatus) -or -not $text.Contains('saveRoot='+$saveRoot)){throw "Player soak did not satisfy its gate: exit=$exitCode status=$($report.status) error=$($report.error)"}
}catch{$failure=$_.Exception.Message}
finally{
    if($process){if(-not $process.HasExited){Stop-Process -Id $process.Id};$process.Dispose()}
    if(Test-Path -LiteralPath $reportPath){
        try{Copy-Item -LiteralPath $reportPath -Destination (Join-Path $evidence 'player-soak.json')}
        catch{$failure=($failure+' PLAYER_REPORT_COPY_FAILED '+$_.Exception.Message).Trim()}
    }
}
$after=Snapshot-PlayerSaves;$unchanged=$before -ceq $after
$endSha=git -C $repo rev-parse HEAD;$shaExit=$LASTEXITCODE
$dirty=@(git -C $repo status --porcelain);$statusExit=$LASTEXITCODE
$worktree=if($dirty.Count){'dirty'}else{'clean'}
if(-not $unchanged){$failure='Normal player saves changed; no repair/overwrite attempted.'}
if($shaExit -ne 0 -or $statusExit -ne 0 -or $sha -ne $endSha){$failure='HEAD changed or git evidence unavailable.'}
if($dirty.Count -and -not $AllowDirty){$failure='Final worktree is dirty.'}
$status=if($failure){'FAIL'}elseif($MemoryAudit){'DIAGNOSTIC_COMPLETE'}elseif($initialDirty.Count -or $dirty.Count){'DEVELOPMENT_PASS'}else{'PASS'}
Write-Manifest $status
Write-Output "FOC_WINDOWS_PLAYER_SOAK_RESULT $status $manifestPath"
if($failure){throw $failure}
