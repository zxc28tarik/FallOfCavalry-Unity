param([Parameter(Mandatory=$true)][string]$UnityEditor,[string]$Blender)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo 'TestResults/HasanDonor/Final'
$checks=[System.Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Force -Path $output | Out-Null
$manifest=Join-Path $output 'validation.json'
$runId=[Guid]::NewGuid().ToString('N')
$startedUtc=[DateTime]::UtcNow
$sha=$null;$suite=$null;$collision=$null;$gate=$null;$activeCheck=$null
$expectedChecks=@('dotnet-restore','dotnet-build','dotnet-test','editmode','pose-samples','corrective-selfcheck','corrective-verify-runtime','windows-review-build','real-windows-motion-captures','production-art')
function Save-ValidationManifest([string]$ExecutionStatus,[string]$Failure){
    $endSha=$null;$dirty=@();$gitError=$null
    try{
        $endSha=git -C $repo rev-parse HEAD
        if($LASTEXITCODE -ne 0){throw 'Cannot read current HEAD'}
        $dirty=@(git -C $repo status --porcelain)
        if($LASTEXITCODE -ne 0){throw 'Cannot read current worktree'}
    }catch{$gitError=$_.Exception.Message}
    $observed=@($checks | ForEach-Object {$_.name})
    $state=[ordered]@{status='NOT READY';executionStatus=$ExecutionStatus;runId=$runId;startedUtc=$startedUtc.ToString('o');updatedUtc=[DateTime]::UtcNow.ToString('o');commitSha=$sha;startSha=$sha;endSha=$endSha;unityVersion='6000.3.16f1';editModePassed=$null;editModeFailed=$null;editModeSkipped=$null;productionArtExitCode=$gate;runtimeCorrectiveInsideVertices=$null;runtimeCorrectiveClearanceViolations=$null;worktree=if($gitError){'unknown'}elseif($dirty.Count){'dirty'}else{'clean'};dirtyPaths=$dirty;gitError=$gitError;acceptedScreenshots=0;checks=@($checks.ToArray());notRun=@($expectedChecks | Where-Object {$_ -notin $observed});error=$Failure}
    if($null -ne $suite){$state.editModePassed=$suite.'test-run'.passed;$state.editModeFailed=$suite.'test-run'.failed;$state.editModeSkipped=$suite.'test-run'.skipped}
    if($null -ne $collision){$state.runtimeCorrectiveInsideVertices=$collision.unresolvedInsideVertices;$state.runtimeCorrectiveClearanceViolations=$collision.unresolvedClearanceViolations}
    $temporary=$manifest+'.'+$runId+'.tmp'
    $state | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $temporary -Encoding utf8
    Move-Item -LiteralPath $temporary -Destination $manifest -Force
}
function Invoke-UnityCheck([string]$Name,[string[]]$Extra){
    $log=Join-Path $output ($Name+'.log')
    $arguments=@('-batchmode','-nographics','-projectPath',('"'+$repo+'/UnityProject"'))+$Extra+@('-logFile',('"'+$log+'"'))
    $script:activeCheck=[pscustomobject]@{name=$Name;command=('"'+$UnityEditor+'" '+($arguments -join ' '));log=$log}
    $p=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    $checks.Add([pscustomobject]@{name=$Name;command=('"'+$UnityEditor+'" '+($arguments -join ' '));exitCode=$p.ExitCode;log=$log})
    $script:activeCheck=$null
    Save-ValidationManifest 'RUNNING' $null
    Write-Host "$Name exit=$($p.ExitCode)"
    return $p.ExitCode
}
Push-Location $repo
try{
    $sha=git rev-parse HEAD
    if($LASTEXITCODE -ne 0){throw 'Cannot resolve final validation start SHA'}
    Save-ValidationManifest 'RUNNING' $null
    $initialDirty=@(git status --porcelain)
    if($LASTEXITCODE -ne 0){throw 'Cannot inspect initial worktree'}
    if($initialDirty.Count){throw 'Final SHA validation requires an initially clean worktree.'}
    foreach($command in @(@('restore','FallOfCavalry.sln'),@('build','FallOfCavalry.sln','--configuration','Release','--no-restore'),@('test','FallOfCavalry.sln','--configuration','Release','--no-build','--no-restore','--logger','trx;LogFileName=14c-hasan-donor.trx'))){
        $log=Join-Path $output ('dotnet-'+$command[0]+'.log')
        $activeCheck=[pscustomobject]@{name=('dotnet-'+$command[0]);command=('dotnet '+($command -join ' '));log=$log}
        & dotnet @command 2>&1 | Tee-Object -FilePath $log | Out-Host
        $code=$LASTEXITCODE;$checks.Add([pscustomobject]@{name=('dotnet-'+$command[0]);command=('dotnet '+($command -join ' '));exitCode=$code;log=$log})
        $activeCheck=$null;Save-ValidationManifest 'RUNNING' $null
        if($code -ne 0){throw "dotnet $($command[0]) failed"}
    }
    $xml=Join-Path $output 'editmode.xml'
    $editModeStartedUtc=[DateTime]::UtcNow
    $editModeCode=Invoke-UnityCheck 'editmode' @('-runTests','-testPlatform','EditMode','-testResults',('"'+$xml+'"'))
    if(-not (Test-Path -LiteralPath $xml) -or (Get-Item -LiteralPath $xml).LastWriteTimeUtc -lt $editModeStartedUtc){throw "EditMode did not produce fresh XML (exit=$editModeCode); old results are not evidence."}
    $suite=[xml](Get-Content -LiteralPath $xml -Raw)
    Save-ValidationManifest 'RUNNING' $null
    if($editModeCode -ne 0){throw "EditMode failed (exit=$editModeCode)"}
    if([int]$suite.'test-run'.failed -ne 0 -or [int]$suite.'test-run'.skipped -ne 0 -or [int]$suite.'test-run'.passed -lt 560){throw 'Test baseline/failure/skip gate failed'}
    if((Invoke-UnityCheck 'pose-samples' @('-quit','-executeMethod','FOC.Editor.Visuals.HasanDonorPoseAuthoring.Export')) -ne 0){throw 'Read-only pose export failed'}
    if([string]::IsNullOrWhiteSpace($Blender)){$Blender=Join-Path $repo 'Artifacts/DccTools/blender-4.5.9-windows-x64/blender.exe'}
    if(-not (Test-Path -LiteralPath $Blender)){throw 'Provide an installed Blender executable for corrective verification.'}
    foreach($check in @('selfcheck','verify-runtime')){
        $arguments=@('--background','--factory-startup','--disable-autoexec','--python-exit-code','1','--python','Tools/Art/solve_hasan_correctives.py','--',('--'+$check))
        $log=Join-Path $output ('corrective-'+$check+'.log')
        $activeCheck=[pscustomobject]@{name=('corrective-'+$check);command=('"'+$Blender+'" '+($arguments -join ' '));log=$log}
        & $Blender @arguments 2>&1 | Tee-Object -FilePath $log | Out-Host
        $code=$LASTEXITCODE;$checks.Add([pscustomobject]@{name=('corrective-'+$check);command=('"'+$Blender+'" '+($arguments -join ' '));exitCode=$code;log=$log})
        $activeCheck=$null;Save-ValidationManifest 'RUNNING' $null
        if($code -ne 0){throw "Corrective $check failed"}
    }
    # Exit 0 means the diagnostic executed, NOT collision or artwork acceptance.
    $collision=Get-Content -LiteralPath (Join-Path $repo 'TestResults/HasanDonor/Correctives/runtime-collision-audit.json') -Raw | ConvertFrom-Json
    if((Invoke-UnityCheck 'windows-review-build' @('-executeMethod','FOC.Editor.Visuals.HistoricalArtReviewBuild.RunHasan')) -ne 0){throw 'Review build failed'}
    $activeCheck=[pscustomobject]@{name='real-windows-motion-captures';command='Tools/Art/Capture-HasanDonor.ps1 -OutputDirectory TestResults/HasanDonor/Final/Captures';log=$null}
    & (Join-Path $PSScriptRoot 'Art/Capture-HasanDonor.ps1') -OutputDirectory (Join-Path $output 'Captures')
    $checks.Add([pscustomobject]@{name='real-windows-motion-captures';command='Tools/Art/Capture-HasanDonor.ps1 -OutputDirectory TestResults/HasanDonor/Final/Captures';exitCode=0})
    $activeCheck=$null;Save-ValidationManifest 'RUNNING' $null
    $gate=Invoke-UnityCheck 'production-art' @('-executeMethod','FOC.Editor.Visuals.ProductionVisualCatalogGate.Run')
    $finalDirty=@(git status --porcelain)
    if($LASTEXITCODE -ne 0){throw 'Cannot inspect final worktree'}
    if($finalDirty.Count){throw 'Validation modified versioned worktree'}
    $finalSha=git rev-parse HEAD
    if($LASTEXITCODE -ne 0 -or $finalSha -ne $sha){throw 'HEAD changed or became unavailable during validation'}
    if($checks.Count -ne $expectedChecks.Count){throw 'Final validation did not execute every expected check'}
    # A technical runner cannot accept artwork or activate the catalog.
    if($gate -ne 0){Save-ValidationManifest 'FAILED' "ProductionArt gate returned exit $gate; technical checks above remain separately recorded.";exit 1}
    Save-ValidationManifest 'COMPLETED' $null
}
catch{
    $failure=$_.Exception.Message
    if($null -ne $activeCheck){$checks.Add([pscustomobject]@{name=$activeCheck.name;command=$activeCheck.command;exitCode=$null;log=$activeCheck.log;outcome='FAILED';error=$failure})}
    Save-ValidationManifest 'FAILED' $failure
    throw
}
finally{Pop-Location}
