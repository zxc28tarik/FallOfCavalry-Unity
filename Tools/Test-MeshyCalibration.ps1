param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$runId=[Guid]::NewGuid().ToString('N');$started=[DateTime]::UtcNow
$sha=$null;$initialDirty=@();$checks=[Collections.Generic.List[object]]::new()
$result='RUNNING';$failure=$null;$baseline=$null;$calibration=$null;$playerEvidence=$null;$existingPipelines=$null
$output=Join-Path $repo ('TestResults/MeshyCalibration/Runs/UNKNOWN/'+$runId)
$expected=@('baseline-pilot','calibration-unity-verify','calibration-windows-player','existing-11-pipelines')

function Read-GitState {
    $current=git -C $repo rev-parse HEAD
    if($LASTEXITCODE -ne 0 -or $current -notmatch '^[0-9a-f]{40}$'){throw 'Cannot resolve current HEAD.'}
    $dirty=@(git -C $repo status --porcelain)
    if($LASTEXITCODE -ne 0){throw 'Cannot inspect worktree.'}
    return [pscustomobject]@{sha=[string]$current;dirty=$dirty}
}
function Save-Run {
    $state=$null;$gitError=$null
    try{$state=Read-GitState}catch{$gitError=$_.Exception.Message}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $done=@($checks | ForEach-Object {$_.name})
    $manifest=[ordered]@{
        result=$result;scope='Technical execution only; NOT calibration, on-foot, or 14C visual acceptance'
        visualAcceptance='NOT_DECIDED_BY_AUTOMATION';runId=$runId;startUtc=$started.ToString('o');updatedUtc=[DateTime]::UtcNow.ToString('o')
        startSha=$sha;endSha=if($state){$state.sha}else{$null};initialDirty=$initialDirty;finalDirty=if($state){$state.dirty}else{$null};gitError=$gitError
        baseline=$baseline;calibration=$calibration;player=$playerEvidence;existingPipelines=$existingPipelines;checks=$checks.ToArray()
        notRun=@($expected | Where-Object {$_ -notin $done});error=$failure;productionActivated=$false
    }
    $temporary=Join-Path $output ('run.'+$runId+'.tmp')
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temporary -Encoding utf8
    Move-Item -LiteralPath $temporary -Destination (Join-Path $output 'run.json') -Force
}
function Format-Command([string]$Program,[string[]]$Arguments){
    return ('& '+((@($Program)+$Arguments | ForEach-Object {"'"+$_.Replace("'","''")+"'"}) -join ' '))
}
function Invoke-Check([string]$Name,[string]$Program,[string[]]$Arguments,[int]$TimeoutSeconds=0,[switch]$NativeConsole){
    $log=Join-Path $output ($Name+'.log');$begin=[DateTime]::UtcNow;$exitCode=$null;$errorText=$null
    $command=Format-Command $Program $Arguments
    try{
        if($NativeConsole){
            & $Program @Arguments 2>&1 | Tee-Object -FilePath $log | Out-Host
            $exitCode=$LASTEXITCODE
        }else{
            # Quote each argv item for Windows without changing the recorded argv array.
            $quoted=@($Arguments | ForEach-Object {'"'+[regex]::Replace([regex]::Replace($_,'(\\*)"','$1$1\"'),'(\\+)$','$1$1')+'"'})
            $process=Start-Process -FilePath $Program -ArgumentList $quoted -WindowStyle Hidden -PassThru
            try{
                if(-not $process.WaitForExit($TimeoutSeconds*1000)){$process.Kill();$process.WaitForExit();throw "$Name timed out after $TimeoutSeconds seconds."}
                $exitCode=$process.ExitCode
            }finally{$process.Dispose()}
        }
        if($exitCode -ne 0){throw "$Name failed: exit=$exitCode"}
    }catch{$errorText=$_.Exception.Message;throw}
    finally{
        $checks.Add([pscustomobject]@{name=$Name;program=$Program;arguments=$Arguments;command=$command;exitCode=$exitCode;log=$log;startUtc=$begin.ToString('o');endUtc=[DateTime]::UtcNow.ToString('o');outcome=if($null -ne $exitCode -and $exitCode -eq 0 -and -not $errorText){'PASS'}else{'FAILED'};error=$errorText})
        Save-Run
    }
}
function Assert-PlayerEvidence($Evidence,[string]$Directory){
    if($Evidence.sourceSha -ne $sha -or $Evidence.platform -ne 'WindowsPlayer' -or $Evidence.activeLeases -ne 0 -or $Evidence.status -ne 'TECHNICAL_CAPTURE_NOT_CALIBRATION_ACCEPTANCE'){throw 'Calibration capture SHA/platform/status/lease mismatch.'}
    $entries=@($Evidence.results)
    if($entries.Count -lt 8 -or $Evidence.captures -ne $entries.Count){throw 'Calibration result/capture count mismatch or missing required samples.'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $entries){
        if(-not $entry.file -or [IO.Path]::GetFileName($entry.file) -ne $entry.file -or [IO.Path]::GetExtension($entry.file) -ne '.png' -or -not $seen.Add($entry.file)){throw 'Invalid or duplicate calibration image filename.'}
        if(-not(Test-Path -LiteralPath (Join-Path $Directory $entry.file) -PathType Leaf)){throw ('Missing fresh calibration image: '+$entry.file)}
        if($entry.realWindowsPlayer -ne $true -or $entry.calibrationScenario -notin @('A','B','C') -or -not $entry.clip -or -not $entry.sourceClipName -or -not $entry.avatarName -or $entry.groundingApplied -isnot [bool]){throw ('Missing truthful scenario/source/clip/avatar/grounding metadata: '+$entry.file)}
        if($null -eq $entry.normalizedPhase -or [double]::IsNaN([double]$entry.normalizedPhase) -or [double]::IsInfinity([double]$entry.normalizedPhase)){throw ('Missing or nonfinite pose phase: '+$entry.file)}
        if($null -eq $entry.animationEvaluationSteps -or [int]$entry.animationEvaluationSteps -lt 1 -or $null -eq $entry.maximumJointTravelMeters -or [double]::IsNaN([double]$entry.maximumJointTravelMeters) -or [double]::IsInfinity([double]$entry.maximumJointTravelMeters) -or [double]$entry.maximumJointTravelMeters -lt 0){throw ('Missing actual animation evaluation count or finite nonnegative travel: '+$entry.file)}
        if(@($entry.poses).Count -lt 1 -or @($entry.poses | Where-Object {$_.renderBoundaryPoseStable -ne $true -or $_.runtimeControllerDetached -ne $true}).Count){throw ('Captured pose was not stable across the render boundary with exclusive manual-graph ownership: '+$entry.file)}
    }
    if(@(Get-ChildItem -LiteralPath $Directory -Filter '*.png' -File).Count -ne $entries.Count){throw 'PNG files do not match the exact calibration result count.'}
    $scenarios=@($Evidence.scenarios)
    if('A' -notin $scenarios -or 'B' -notin $scenarios -or 'C' -notin $scenarios -or @($scenarios | Where-Object {$_ -notin @('A','B','C')}).Count -or @($scenarios | Select-Object -Unique).Count -ne $scenarios.Count){throw 'Final calibration closure requires the full A/B/C scenario inventory.'}
    if(@(Compare-Object @($entries.calibrationScenario | Sort-Object -Unique) @($scenarios | Sort-Object)).Count){throw 'Scenario inventory does not match captured results.'}
    foreach($scenario in $scenarios){
        foreach($motion in @('Idle','Turn','OneHandedAttack','ArmRaise','Crouch','MountedSeated')){
            foreach($phase in @(.25,.5,.75)){
                $matches=@($entries | Where-Object {$_.calibrationScenario -eq $scenario -and $_.clip.EndsWith('_'+$motion,[StringComparison]::Ordinal) -and [Math]::Abs([double]$_.normalizedPhase-$phase) -lt .00001})
                if($matches.Count -ne 1){throw "Expected exactly one $scenario/$motion/$phase diagnostic capture."}
            }
        }
    }
    foreach($scenario in @('A','B')){foreach($motion in @('Walking','Running')){foreach($phase in @(.25,.75)){
        $matches=@($entries | Where-Object {$_.calibrationScenario -eq $scenario -and $_.clip -eq $motion -and $_.groundingApplied -eq $false -and [Math]::Abs([double]$_.normalizedPhase-$phase) -lt .00001})
        if($matches.Count -ne 1){throw "Expected exactly one raw $scenario/$motion/$phase control."}
    }}}
    return [pscustomobject]@{sourceSha=$Evidence.sourceSha;platform=$Evidence.platform;captures=$entries.Count;scenarios=$scenarios;groundingAppliedCaptures=@($entries | Where-Object {$_.groundingApplied}).Count;visualAcceptance='NOT_DECIDED_BY_AUTOMATION';contactStatus=$Evidence.contactStatus;mountStatus=$Evidence.mountStatus;benchmarkStatus=$Evidence.benchmarkStatus}
}
function Archive-ExistingEvidence([DateTime]$Since){
    $target=Join-Path $output 'ExistingPipelines'
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $repo 'TestResults') -File){
        if($file.Extension -in @('.log','.xml','.json') -and $file.LastWriteTimeUtc -ge $Since){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name)}
    }
}

Push-Location $repo
try{
    $initial=Read-GitState;$sha=$initial.sha;$initialDirty=$initial.dirty
    $output=Join-Path $repo ('TestResults/MeshyCalibration/Runs/'+$sha+'/'+$runId)
    Save-Run
    if($initialDirty.Count){throw 'Final calibration evidence requires an initially clean worktree.'}
    if(-not(Test-Path -LiteralPath $UnityEditor -PathType Leaf)){throw 'Unity Editor executable was not found.'}
    $shell=(Get-Command pwsh -ErrorAction Stop).Source
    $baselineRoot=Join-Path $repo ('TestResults/MeshyPilot/Runs/'+$sha)
    $previous=@(if(Test-Path -LiteralPath $baselineRoot){Get-ChildItem -LiteralPath $baselineRoot -Directory | ForEach-Object {$_.Name}})
    try{Invoke-Check 'baseline-pilot' $shell @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Test-MeshyPilot.ps1'),'-UnityEditor',$UnityEditor) -NativeConsole}
    finally{
        $fresh=@(if(Test-Path -LiteralPath $baselineRoot){Get-ChildItem -LiteralPath $baselineRoot -Directory | Where-Object {$_.Name -notin $previous}})
        if($fresh.Count -eq 1){
            $baseline=[pscustomobject]@{directory=$fresh[0].FullName;manifestAvailable=$false;run=$null}
            $baselinePath=Join-Path $fresh[0].FullName 'run.json'
            if(Test-Path -LiteralPath $baselinePath){$baseline.run=Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json;$baseline.manifestAvailable=$true;Copy-Item -LiteralPath $baselinePath -Destination (Join-Path $output 'baseline-run.json')}
        }
        Save-Run
    }
    if(-not $baseline -or -not $baseline.manifestAvailable){throw 'Exactly one fresh baseline run manifest is required.'}
    $b=$baseline.run
    if($b.result -ne 'PASS' -or $b.startSha -ne $sha -or $b.endSha -ne $sha -or $b.diagnosticDirtyRun -or @($b.initialDirty).Count -or @($b.finalDirty).Count){throw 'Baseline is not clean, successful final-SHA evidence.'}
    if($b.dotnet.passed -lt 486 -or $b.dotnet.passed -ne $b.dotnet.total -or $b.dotnet.failed -ne 0 -or $b.dotnet.notExecuted -ne 0 -or $b.unity.passed -lt 659 -or $b.unity.passed -ne $b.unity.total -or $b.unity.failed -ne 0 -or $b.unity.skipped -ne 0){throw 'Baseline test floor, failure, or skip gate failed.'}
    if(@($b.checks | Where-Object {$null -eq $_.exitCode -or $_.exitCode -ne 0}).Count){throw 'Baseline contains an unexecuted or failed check.'}
    [xml]$baselineXml=Get-Content -LiteralPath (Join-Path $baseline.directory 'unity-editmode.xml') -Raw
    $calibrationCases=@(Select-Xml -Xml $baselineXml -XPath '//test-case[contains(@fullname,".MeshyHumanoidCalibrationTests.")]')
    $lifecycleCases=@(Select-Xml -Xml $baselineXml -XPath '//test-case[contains(@fullname,".MeshyCalibrationPlayerTests.")]')
    if($calibrationCases.Count -ne 13 -or @($calibrationCases | Where-Object {$_.Node.result -ne 'Passed'}).Count -or $lifecycleCases.Count -ne 2 -or @($lifecycleCases | Where-Object {$_.Node.result -ne 'Passed'}).Count -or $b.unity.passed -lt 674){throw 'Exactly 13 calibration + 2 player lifecycle cases must be discovered and pass, with all 659 prior tests preserved (at least 674 total).'}
    $baseline | Add-Member -NotePropertyName calibrationTests -NotePropertyValue ([pscustomobject]@{passed=$calibrationCases.Count;expected=13;playerLifecyclePassed=$lifecycleCases.Count;playerLifecycleExpected=2;failedOrSkipped=0})
    $verifyStart=[DateTime]::UtcNow
    $verifyLog=Join-Path $output 'calibration-unity-verify.log'
    $reportPath=Join-Path $repo 'TestResults/MeshyCalibration/calibration.json'
    try{Invoke-Check 'calibration-unity-verify' $UnityEditor @('-batchmode','-nographics','-projectPath',(Join-Path $repo 'UnityProject'),'-executeMethod','FOC.Editor.Visuals.MeshyHumanoidCalibration.RunVerify','-logFile',$verifyLog) -TimeoutSeconds 900}
    finally{
        if((Test-Path -LiteralPath $reportPath) -and (Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -ge $verifyStart){
            Copy-Item -LiteralPath $reportPath -Destination (Join-Path $output 'calibration.json')
            $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            $calibration=[pscustomobject]@{status=$report.status;operation=$report.operation;utc=$report.utc;inputAssetsUnchanged=$report.inputAssetsUnchanged;scenarios=@($report.scenarios.id);report=(Join-Path $output 'calibration.json');error=$report.error;visualAcceptance='NOT_DECIDED_BY_AUTOMATION'}
        }
        Save-Run
    }
    if(-not $calibration -or $calibration.status -ne 'CALIBRATION_EXPERIMENT_COMPLETE_VISUAL_ACCEPTANCE_PENDING' -or $calibration.operation -ne 'Verify' -or $calibration.inputAssetsUnchanged -ne $true -or $calibration.error -or 'A' -notin $calibration.scenarios -or 'B' -notin $calibration.scenarios -or 'C' -notin $calibration.scenarios){throw 'Fresh read-only A/B/C calibration report missing or invalid; process exit alone is insufficient.'}
    $capture=Join-Path $output 'CalibrationCaptures'
    $player=Join-Path $repo 'Artifacts/MeshyPilotPlayer/FallOfCavalry-MeshyPilot.exe'
    if(-not(Test-Path -LiteralPath $player -PathType Leaf)){throw 'Fresh baseline Windows player build is missing.'}
    $playerLog=Join-Path $output 'calibration-windows-player.log'
    Invoke-Check 'calibration-windows-player' $player @('-screen-fullscreen','0','-screen-width','1600','-screen-height','1100','--meshy-calibration','true','--meshy-output',$capture,'--meshy-sha',$sha,'-logFile',$playerLog) -TimeoutSeconds 300
    if(-not(Test-Path -LiteralPath $playerLog) -or (Select-String -LiteralPath $playerLog -Pattern '(^|\s)((?:[A-Za-z_][A-Za-z0-9_.+]*)?Exception:|Error:)' -Quiet)){throw 'Calibration Windows player log missing or reports an exception/error.'}
    $evidence=Get-Content -LiteralPath (Join-Path $capture 'calibration-player-evidence.json') -Raw | ConvertFrom-Json
    $playerEvidence=Assert-PlayerEvidence $evidence $capture
    if(@(Compare-Object @($calibration.scenarios | Sort-Object) @($playerEvidence.scenarios | Sort-Object)).Count){throw 'Editor and Windows calibration scenario inventories differ.'}
    Save-Run
    $existingStart=[DateTime]::UtcNow
    $existingPath=Join-Path $repo 'TestResults/14c-existing-pipelines.json'
    $previousExistingId=if(Test-Path -LiteralPath $existingPath){(Get-Content -LiteralPath $existingPath -Raw | ConvertFrom-Json).runId}else{$null}
    try{Invoke-Check 'existing-11-pipelines' $shell @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Test-14CExistingPipelines.ps1'),'-UnityEditor',$UnityEditor) -NativeConsole}
    finally{
        Archive-ExistingEvidence $existingStart
        if((Test-Path -LiteralPath $existingPath) -and (Get-Item -LiteralPath $existingPath).LastWriteTimeUtc -ge $existingStart){$existingPipelines=Get-Content -LiteralPath $existingPath -Raw | ConvertFrom-Json}
        Save-Run
    }
    $pipelineNames=@('Test-VisualPipeline','Test-BattlePipeline','Test-EncounterContractPipeline','Test-AIPipeline','Test-PresentationPipeline','Test-IntegrationPipeline','Test-WorldMapPipeline','Test-HistoricalContentPipeline','Test-SaveHardeningPipeline','Test-LongRunPipeline','Build-Windows-Development')
    if(-not $existingPipelines -or $existingPipelines.runId -eq $previousExistingId -or $existingPipelines.executionStatus -ne 'COMPLETED' -or $existingPipelines.startSha -ne $sha -or $existingPipelines.endSha -ne $sha -or $existingPipelines.worktree -ne 'clean' -or @($existingPipelines.notRun).Count -or @($existingPipelines.checks).Count -ne 11){throw 'All 11 existing pipelines require fresh, successful clean-SHA evidence.'}
    if(@(Compare-Object $pipelineNames @($existingPipelines.checks.name)).Count -or @($existingPipelines.checks | Where-Object {$null -eq $_.exitCode -or $_.exitCode -ne 0}).Count){throw 'Existing pipeline inventory/return codes do not pass.'}
    foreach($innerLog in @('visual-pipeline.log','windows-development-build.log')){
        $archived=Join-Path (Join-Path $output 'ExistingPipelines') $innerLog
        if(-not(Test-Path -LiteralPath $archived) -or (Get-Item -LiteralPath $archived).Length -eq 0){throw ('Missing fresh inner log; empty wrapper stdout is not evidence: '+$innerLog)}
    }
    $final=Read-GitState
    if($final.sha -ne $sha -or $final.dirty.Count){throw 'HEAD/worktree changed during final calibration validation.'}
    $result='PASS'
}catch{$result='FAILED';$failure=$_.Exception.Message;Write-Warning $failure}
finally{
    try{$final=Read-GitState;if($final.sha -ne $sha -or $final.dirty.Count){$result='FAILED';$guard='Final HEAD/worktree guard failed.';$failure=if($failure){$failure+' '+$guard}else{$guard}}}catch{$result='FAILED';$failure=if($failure){$failure+' '+$_.Exception.Message}else{$_.Exception.Message}}
    Save-Run
    Pop-Location
}
Write-Output "MESHY_CALIBRATION_TECHNICAL_RESULT $result evidence=$output"
if($result -ne 'PASS'){exit 1}
