param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [switch]$AllowDirtyDiagnostic,
    [switch]$RunBenchmark
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$sha=git -C $repo rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Cannot resolve HEAD.'}
$dirty=@(git -C $repo status --porcelain)
if($dirty.Count -and -not $AllowDirtyDiagnostic){throw 'Final evidence requires a clean initial worktree.'}
$runId=[Guid]::NewGuid().ToString('N')
$output=Join-Path $repo ('TestResults/MeshyPilot/Runs/'+$sha+'/'+$runId)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$checks=[Collections.Generic.List[object]]::new()
$start=[DateTime]::UtcNow
$errorMessage=$null
$result='FAILED'
$dotnetCounts=$null
$unityCounts=$null
function Invoke-NativeCheck([string]$Name,[string]$Program,[string[]]$Arguments){
    $log=Join-Path $output ($Name+'.log')
    $command=$Program+' '+($Arguments -join ' ')
    & $Program @Arguments 2>&1 | Tee-Object -FilePath $log | Out-Host
    $exitCode=$LASTEXITCODE
    $checks.Add([pscustomobject]@{name=$Name;command=$command;exitCode=$exitCode;log=$log})
    if($exitCode -ne 0){throw "$Name failed: exit=$exitCode"}
}
function Invoke-UnityCheck([string]$Name,[string[]]$Arguments){
    $log=Join-Path $output ($Name+'.log')
    $arguments=@('-batchmode','-nographics','-projectPath',('"'+(Join-Path $repo 'UnityProject')+'"'),'-logFile',('"'+$log+'"'))+$Arguments
    $process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(900000)){$process.Kill();throw "$Name timed out."}
    $checks.Add([pscustomobject]@{name=$Name;command=('"'+$UnityEditor+'" '+($arguments -join ' '));exitCode=$process.ExitCode;log=$log})
    if($process.ExitCode -ne 0){throw "$Name failed: exit=$($process.ExitCode)"}
}
Push-Location $repo
try {
    Invoke-NativeCheck 'dotnet-restore' 'dotnet' @('restore','FallOfCavalry.sln')
    Invoke-NativeCheck 'dotnet-build' 'dotnet' @('build','FallOfCavalry.sln','--configuration','Release','--no-restore')
    Invoke-NativeCheck 'dotnet-test' 'dotnet' @('test','FallOfCavalry.sln','--configuration','Release','--no-build','--no-restore','--logger','trx','--results-directory',$output)
    $trxFiles=@(Get-ChildItem -LiteralPath $output -Filter '*.trx')
    if($trxFiles.Count -ne 1){throw 'Expected one freshly produced .NET test result file.'}
    [xml]$trx=Get-Content -LiteralPath $trxFiles[0].FullName -Raw
    $counters=$trx.TestRun.ResultSummary.Counters
    $dotnetCounts=@{passed=[int]$counters.passed;failed=[int]$counters.failed;notExecuted=[int]$counters.notExecuted;total=[int]$counters.total}
    if($dotnetCounts.passed -lt 1 -or $dotnetCounts.passed -ne $dotnetCounts.total){throw '.NET tests contain failures, skips, or no tests.'}
    Invoke-UnityCheck 'unity-verify' @('-executeMethod','FOC.Editor.Visuals.MeshyHasanPilotPipeline.RunVerify')
    Copy-Item -LiteralPath (Join-Path $repo 'TestResults/MeshyPilot/unity-import.json') -Destination (Join-Path $output 'unity-import.json')
    Invoke-UnityCheck 'unity-motion-verify' @('-executeMethod','FOC.Editor.Visuals.MeshyHasanPilotMotionAdaptation.RunVerify')
    Copy-Item -LiteralPath (Join-Path $repo 'TestResults/MeshyPilot/motion-adaptation.json') -Destination (Join-Path $output 'motion-adaptation.json')
    Invoke-UnityCheck 'unity-motion-roundtrip' @('-executeMethod','FOC.Editor.Visuals.MeshyHasanPilotRoundtripAudit.Run')
    Copy-Item -LiteralPath (Join-Path $repo 'TestResults/MeshyPilot/motion-roundtrip.json') -Destination (Join-Path $output 'motion-roundtrip.json')
    $xmlPath=Join-Path $output 'unity-editmode.xml'
    Invoke-UnityCheck 'unity-editmode' @('-runTests','-testPlatform','EditMode','-testResults',('"'+$xmlPath+'"'))
    [xml]$xml=Get-Content -LiteralPath $xmlPath -Raw
    $tests=$xml.'test-run'
    $unityCounts=@{passed=[int]$tests.passed;failed=[int]$tests.failed;skipped=[int]$tests.skipped;total=[int]$tests.total}
    if($tests.result -ne 'Passed' -or [int]$tests.failed -ne 0 -or [int]$tests.skipped -ne 0 -or [int]$tests.passed -lt 1 -or [int]$tests.passed -ne [int]$tests.total){throw 'EditMode failure, skip, inconclusive, or no test results.'}
    $pilotCases=@(Select-Xml -Xml $xml -XPath '//test-case[starts-with(@fullname,"FOC.Tests.VisualPipeline.MeshyHasanPilotTests.")]')
    if($pilotCases.Count -ne 17 -or @($pilotCases | Where-Object {$_.Node.result -ne 'Passed'}).Count -ne 0){throw 'All 17 Meshy pilot cases must actually be discovered and pass.'}
    $unityCounts.meshyPilotPassed=$pilotCases.Count
    foreach($suite in @(@{name='MeshyHasanPilotMotionAdaptationTests';count=9},@{name='MeshyHasanPilotBenchmarkTests';count=3})){
        $suiteCases=@(Select-Xml -Xml $xml -XPath ('//test-case[starts-with(@fullname,"FOC.Tests.VisualPipeline.'+$suite.name+'.")]'))
        if($suiteCases.Count -ne $suite.count -or @($suiteCases | Where-Object {$_.Node.result -ne 'Passed'}).Count -ne 0){throw ('All '+$suite.count+' '+$suite.name+' cases must actually be discovered and pass.')}
        $unityCounts[$suite.name]=$suiteCases.Count
    }
    Invoke-UnityCheck 'unity-player-build' @('-executeMethod','FOC.Editor.Visuals.MeshyHasanPilotBuild.Run')
    $player=Join-Path $repo 'Artifacts/MeshyPilotPlayer/FallOfCavalry-MeshyPilot.exe'
    $capture=Join-Path $output 'Captures'
    $playerLog=Join-Path $output 'windows-player.log'
    $playerArguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','1000','--meshy-output',('"'+$capture+'"'),'--meshy-sha',$sha,'-logFile',('"'+$playerLog+'"'))
    if($RunBenchmark){$playerArguments+=@('--meshy-run-benchmark','true')}
    $process=Start-Process -FilePath $player -ArgumentList $playerArguments -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(240000)){$process.Kill();throw 'Windows pilot capture timed out.'}
    $checks.Add([pscustomobject]@{name='windows-player';command=('"'+$player+'" '+($playerArguments -join ' '));exitCode=$process.ExitCode;log=$playerLog})
    if($process.ExitCode -ne 0){throw 'Windows pilot capture returned nonzero.'}
    if(Select-String -LiteralPath $playerLog -Pattern '(^|\s)((?:[A-Za-z_][A-Za-z0-9_.+]*)?Exception:|Error:)' -Quiet){throw 'Windows player runtime exception/error.'}
    if(@(Get-ChildItem -LiteralPath $capture -Filter '*.png').Count -ne 37){throw 'Missing real Windows captures.'}
    $playerEvidence=Get-Content -LiteralPath (Join-Path $capture 'player-evidence.json') -Raw | ConvertFrom-Json
    if($playerEvidence.sourceSha -ne $sha -or $playerEvidence.platform -ne 'WindowsPlayer' -or $playerEvidence.captures -ne 37 -or $playerEvidence.activeLeases -ne 0){throw 'Player evidence SHA/platform/capture/lease mismatch.'}
    if($RunBenchmark -and @($playerEvidence.performance.cases).Count -ne 3){throw 'Missing requested 1/12/100 runtime measurements.'}
    $result='PASS'
} catch { $errorMessage=$_.Exception.Message; Write-Warning $errorMessage }
finally {
    $endSha=git -C $repo rev-parse HEAD
    $endDirty=@(git -C $repo status --porcelain)
    if($endSha -ne $sha){$result='FAILED';$errorMessage='HEAD changed during run.'}
    if($endDirty.Count -and -not $AllowDirtyDiagnostic){$result='FAILED';$errorMessage='Worktree changed during final evidence run.'}
    [ordered]@{result=$result;scope='Technical execution only, NOT visual pilot or complete 14C acceptance';visualAcceptance='NOT_DECIDED_BY_AUTOMATION';runId=$runId;startUtc=$start.ToString('o');endUtc=[DateTime]::UtcNow.ToString('o');startSha=$sha;endSha=$endSha;diagnosticDirtyRun=[bool]$AllowDirtyDiagnostic;initialDirty=$dirty;finalDirty=$endDirty;dotnet=$dotnetCounts;unity=$unityCounts;checks=$checks.ToArray();error=$errorMessage;productionActivated=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'run.json') -Encoding utf8
    Pop-Location
}
Write-Output "MESHY_PILOT_TECHNICAL_RESULT $result evidence=$output"
if($result -ne 'PASS'){exit 1}
