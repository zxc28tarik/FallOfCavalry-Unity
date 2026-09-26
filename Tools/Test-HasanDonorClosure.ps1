param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo 'TestResults/HasanDonor/Final'
$checks=[System.Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Force -Path $output | Out-Null
function Invoke-UnityCheck([string]$Name,[string[]]$Extra){
    $log=Join-Path $output ($Name+'.log')
    $arguments=@('-batchmode','-nographics','-projectPath',('"'+$repo+'/UnityProject"'))+$Extra+@('-logFile',('"'+$log+'"'))
    $p=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    $checks.Add([pscustomobject]@{name=$Name;command=('"'+$UnityEditor+'" '+($arguments -join ' '));exitCode=$p.ExitCode;log=$log})
    Write-Host "$Name exit=$($p.ExitCode)"
    return $p.ExitCode
}
Push-Location $repo
try{
    if(git status --porcelain){throw 'Final SHA validation requires an initially clean worktree.'}
    $sha=git rev-parse HEAD
    foreach($command in @(@('restore','FallOfCavalry.sln'),@('build','FallOfCavalry.sln','--configuration','Release','--no-restore'),@('test','FallOfCavalry.sln','--configuration','Release','--no-build','--no-restore','--logger','trx;LogFileName=14c-hasan-donor.trx'))){
        $log=Join-Path $output ('dotnet-'+$command[0]+'.log')
        & dotnet @command 2>&1 | Tee-Object -FilePath $log | Out-Host
        $code=$LASTEXITCODE;$checks.Add([pscustomobject]@{name=('dotnet-'+$command[0]);command=('dotnet '+($command -join ' '));exitCode=$code;log=$log})
        if($code -ne 0){throw "dotnet $($command[0]) failed"}
    }
    $xml=Join-Path $output 'editmode.xml'
    if((Invoke-UnityCheck 'editmode' @('-runTests','-testPlatform','EditMode','-testResults',('"'+$xml+'"'))) -ne 0){throw 'EditMode failed'}
    $suite=[xml](Get-Content -LiteralPath $xml -Raw)
    if([int]$suite.'test-run'.failed -ne 0 -or [int]$suite.'test-run'.skipped -ne 0 -or [int]$suite.'test-run'.passed -lt 560){throw 'Test baseline/failure/skip gate failed'}
    if((Invoke-UnityCheck 'windows-review-build' @('-executeMethod','FOC.Editor.Visuals.HistoricalArtReviewBuild.RunHasan')) -ne 0){throw 'Review build failed'}
    & (Join-Path $PSScriptRoot 'Art/Capture-HasanDonor.ps1') -OutputDirectory (Join-Path $output 'Captures')
    $checks.Add([pscustomobject]@{name='real-windows-motion-captures';command='Tools/Art/Capture-HasanDonor.ps1 -OutputDirectory TestResults/HasanDonor/Final/Captures';exitCode=0})
    $gate=Invoke-UnityCheck 'production-art' @('-executeMethod','FOC.Editor.Visuals.ProductionVisualCatalogGate.Run')
    if(git status --porcelain){throw 'Validation modified versioned worktree'}
    if((git rev-parse HEAD) -ne $sha){throw 'HEAD changed during validation'}
    [pscustomobject]@{status='NOT READY';commitSha=$sha;endSha=(git rev-parse HEAD);unityVersion='6000.3.16f1';editModePassed=$suite.'test-run'.passed;editModeFailed=$suite.'test-run'.failed;editModeSkipped=$suite.'test-run'.skipped;productionArtExitCode=$gate;worktree='clean';acceptedScreenshots=0;checks=$checks} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'validation.json') -Encoding utf8
    # A technical runner cannot accept artwork or activate the catalog.
    if($gate -ne 0){exit 1}
}
finally{Pop-Location}
