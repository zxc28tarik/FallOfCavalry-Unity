param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$results=Join-Path $repo 'TestResults'
$checks=[System.Collections.Generic.List[object]]::new()
Push-Location $repo
function Invoke-RescueUnity([string]$Name,[string[]]$Extra){
    $log=Join-Path $results ('14c-rescue-final-'+$Name+'.log')
    $arguments=@('-batchmode','-nographics','-projectPath',('"'+(Join-Path $repo 'UnityProject')+'"'))+$Extra+@('-logFile',('"'+$log+'"'))
    $p=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $checks.Add([pscustomobject]@{name=$Name;command=('"'+$UnityEditor+'" '+($arguments -join ' '));exitCode=$p.ExitCode;log=$log})
    return $p.ExitCode
}
try{
    if(git status --porcelain){throw 'Final-SHA evidence requires clean initial worktree.'}
    $sha=git rev-parse HEAD
    New-Item -ItemType Directory -Force -Path $results | Out-Null
    foreach($command in @(
        @('restore','FallOfCavalry.sln'),
        @('build','FallOfCavalry.sln','--configuration','Release','--no-restore'),
        @('test','FallOfCavalry.sln','--configuration','Release','--no-build','--no-restore','--logger','trx;LogFileName=implementation-14c-rescue.trx')
    )){
        $log=Join-Path $results ('14c-rescue-final-dotnet-'+$command[0]+'.log')
        & dotnet @command 2>&1 | Tee-Object -FilePath $log | Out-Host
        $code=$LASTEXITCODE
        $checks.Add([pscustomobject]@{name=('dotnet-'+$command[0]);command=('dotnet '+($command -join ' '));exitCode=$code;log=$log})
        if($code -ne 0){throw "dotnet $($command[0]) failed"}
    }
    $xml=Join-Path $results '14c-rescue-final-editmode.xml'
    if((Invoke-RescueUnity 'editmode' @('-runTests','-testPlatform','EditMode','-testResults',('"'+$xml+'"'))) -ne 0){throw 'EditMode failed'}
    if((Invoke-RescueUnity 'review-build' @('-executeMethod','FOC.Editor.Visuals.HistoricalArtReviewBuild.Run')) -ne 0){throw 'Review build failed'}
    & (Join-Path $PSScriptRoot 'Art\Capture-HasanRescue.ps1') -OutputDirectory (Join-Path $results '14c-rescue-final-captures')
    $checks.Add([pscustomobject]@{name='hasan-three-diagnostic-captures';command='Tools/Art/Capture-HasanRescue.ps1 -OutputDirectory TestResults/14c-rescue-final-captures';exitCode=0})
    # This is not an accepted-art or prefab-only runtime benchmark.
    $gate=Invoke-RescueUnity 'production-art-gate' @('-executeMethod','FOC.Editor.Visuals.ProductionVisualCatalogGate.Run')
    if(git status --porcelain){throw 'Validation dirtied worktree'}
    if((git rev-parse HEAD) -ne $sha){throw 'HEAD changed during validation'}
    $suite=[xml](Get-Content -LiteralPath $xml -Raw)
    $summary=[pscustomobject]@{status='NOT READY';branch=(git branch --show-current);commitSha=$sha;endSha=(git rev-parse HEAD);editModePassed=$suite.'test-run'.passed;editModeFailed=$suite.'test-run'.failed;editModeSkipped=$suite.'test-run'.skipped;productionArtGateExitCode=$gate;acceptedScreenshots=0;fullRuntimeBenchmark='Not Run: human art acceptance failed; catalog not activated';worktree='clean';checks=$checks}
    $summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $results '14c-rescue-final-validation.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 7 | Out-Host
    if($gate -ne 0){exit 1}
    # This technical runner can never promote art or authorize Implementation 15.
}
finally{Pop-Location}
