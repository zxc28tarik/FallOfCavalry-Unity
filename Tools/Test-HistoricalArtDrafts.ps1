param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Push-Location $repo
$checks=[System.Collections.Generic.List[object]]::new()
$sha=git rev-parse HEAD
$branch=git branch --show-current
$results=Join-Path $repo 'TestResults'
New-Item -ItemType Directory -Force -Path $results | Out-Null
function Invoke-UnityCheck([string]$Name,[string[]]$Extra){
    $log=Join-Path $results ('14c-final-'+$Name+'.log')
    $arguments=@('-batchmode','-nographics','-projectPath',('"'+(Join-Path $repo 'UnityProject')+'"'))+$Extra+@('-logFile',('"'+$log+'"'))
    $p=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $checks.Add([pscustomobject]@{name=$Name;command=('"'+$UnityEditor+'" '+($arguments -join ' '));exitCode=$p.ExitCode;log=$log})
    return $p.ExitCode
}
try{
    if(git status --porcelain){throw 'Final-SHA evidence requires an initially clean worktree.'}
    foreach($command in @(
        @('restore','FallOfCavalry.sln'),
        @('build','FallOfCavalry.sln','--configuration','Release','--no-restore'),
        @('test','FallOfCavalry.sln','--configuration','Release','--no-build','--no-restore','--logger','trx;LogFileName=implementation-14c-final.trx')
    )){
        $log=Join-Path $results ('14c-final-dotnet-'+$command[0]+'.log')
        & dotnet @command 2>&1 | Tee-Object -FilePath $log | Out-Host
        $code=$LASTEXITCODE
        $checks.Add([pscustomobject]@{name=('dotnet-'+$command[0]);command=('dotnet '+($command -join ' '));exitCode=$code;log=$log})
        if($code -ne 0){throw "dotnet $($command[0]) failed"}
    }
    $xml=Join-Path $results '14c-final-editmode.xml'
    if((Invoke-UnityCheck 'editmode' @('-runTests','-testPlatform','EditMode','-testResults',('"'+$xml+'"'))) -ne 0){throw 'Unity EditMode failed.'}
    foreach($item in @(
        @('review-build','FOC.Editor.Visuals.HistoricalArtReviewBuild.Run'),
        @('draft-benchmark','FOC.Editor.Visuals.HistoricalArtDraftBenchmark.Run')
    )){
        if((Invoke-UnityCheck $item[0] @('-executeMethod',$item[1])) -ne 0){throw "$($item[0]) failed"}
    }
    & (Join-Path $PSScriptRoot 'Art\Capture-HistoricalArtDrafts.ps1') -OutputDirectory (Join-Path $results '14c-final-captures')
    $checks.Add([pscustomobject]@{name='six-draft-player-captures';command='Tools/Art/Capture-HistoricalArtDrafts.ps1 -OutputDirectory TestResults/14c-final-captures';exitCode=0;log='TestResults/14c-*-player.log'})
    $gate=Invoke-UnityCheck 'production-art-gate' @('-executeMethod','FOC.Editor.Visuals.ProductionVisualCatalogGate.Run')
    $after=git status --porcelain
    if($after){throw ('Validation dirtied the worktree: '+($after -join '; '))}
    $suite=[xml](Get-Content -LiteralPath $xml -Raw)
    $summary=[pscustomobject]@{status='NOT READY';implementation='14C draft checkpoint';branch=$branch;commitSha=$sha;endSha=(git rev-parse HEAD);unityVersion=(Get-Item -LiteralPath $UnityEditor).VersionInfo.FileVersion;editModePassed=$suite.'test-run'.passed;editModeFailed=$suite.'test-run'.failed;editModeSkipped=$suite.'test-run'.skipped;productionArtGateExitCode=$gate;worktree='clean';checks=$checks;note='Six draft review captures are not the ten mandatory assembled production acceptance captures. Not authorized for Implementation 15.'}
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $results '14c-final-validation.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 6 | Out-Host
    if($gate -ne 0){exit 1}
}
finally{Pop-Location}
