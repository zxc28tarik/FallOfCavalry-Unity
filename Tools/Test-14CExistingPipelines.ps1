param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$results=Join-Path $repo 'TestResults'
New-Item -ItemType Directory -Force -Path $results | Out-Null
$sha=$null
$checks=[System.Collections.Generic.List[object]]::new()
$failed=$false
$runId=[Guid]::NewGuid().ToString('N');$startedUtc=[DateTime]::UtcNow;$activeCheck=$null
$manifest=Join-Path $results '14c-existing-pipelines.json'
$expectedChecks=@('Test-VisualPipeline','Test-BattlePipeline','Test-EncounterContractPipeline','Test-AIPipeline','Test-PresentationPipeline','Test-IntegrationPipeline','Test-WorldMapPipeline','Test-HistoricalContentPipeline','Test-SaveHardeningPipeline','Test-LongRunPipeline','Build-Windows-Development')
function Save-PipelineManifest([string]$ExecutionStatus,[string]$Failure){
    $endSha=$null;$dirty=@();$gitError=$null
    try{
        $endSha=git -C $repo rev-parse HEAD
        if($LASTEXITCODE -ne 0){throw 'Cannot read current HEAD'}
        $dirty=@(git -C $repo status --porcelain)
        if($LASTEXITCODE -ne 0){throw 'Cannot read current worktree'}
    }catch{$gitError=$_.Exception.Message}
    $observed=@($checks | ForEach-Object {$_.name})
    $temporary=$manifest+'.'+$runId+'.tmp'
    [ordered]@{status='NOT READY';executionStatus=$ExecutionStatus;runId=$runId;startedUtc=$startedUtc.ToString('o');updatedUtc=[DateTime]::UtcNow.ToString('o');commitSha=$sha;startSha=$sha;endSha=$endSha;checks=@($checks.ToArray());notRun=@($expectedChecks | Where-Object {$_ -notin $observed});worktree=if($gitError){'unknown'}elseif($dirty.Count){'dirty'}else{'clean'};dirtyPaths=$dirty;gitError=$gitError;error=$Failure} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding utf8
    Move-Item -LiteralPath $temporary -Destination $manifest -Force
}
try{
$sha=git -C $repo rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Cannot resolve existing-pipeline start SHA'}
Save-PipelineManifest 'RUNNING' $null
$initialDirty=@(git -C $repo status --porcelain)
if($LASTEXITCODE -ne 0){throw 'Cannot inspect initial worktree'}
if($initialDirty.Count){throw 'Final SHA pipeline validation requires an initially clean worktree.'}
$shell=(Get-Command pwsh -ErrorAction Stop).Source
foreach($name in $expectedChecks){
    $script=Join-Path $PSScriptRoot ($name+'.ps1')
    $command="& '"+$script.Replace("'","''")+"'"
    if($name -notin @('Test-SaveHardeningPipeline','Test-LongRunPipeline')){$command+=" -UnityEditor '"+$UnityEditor.Replace("'","''")+"'"}
    # Existing scripts remain unchanged. Set the native helper visibility in the
    # child session; preserve each script's exact filter, build and exit behavior.
    $body='$ErrorActionPreference=''Stop''; try { $PSDefaultParameterValues[''Start-Process:WindowStyle'']=''Hidden''; '+$command+'; exit $LASTEXITCODE } catch { [Console]::Error.WriteLine($_.Exception.ToString()); exit 1 }'
    $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($body))
    $log=Join-Path $results ('14c-existing-'+$name+'.log')
    $activeCheck=[pscustomobject]@{name=$name;command=$command;log=$log}
    & $shell -NoProfile -EncodedCommand $encoded 2>&1 | Tee-Object -FilePath $log | Out-Host
    $code=$LASTEXITCODE
    $checks.Add([pscustomobject]@{name=$name;command=$command;exitCode=$code;log=$log})
    $activeCheck=$null
    if($code -ne 0){$failed=$true}
    Save-PipelineManifest 'RUNNING' $null
    Write-Output "FOC_14C_EXISTING_PIPELINE $name exit=$code sha=$sha"
}
$finalDirty=@(git -C $repo status --porcelain)
if($LASTEXITCODE -ne 0){throw 'Cannot inspect final worktree'}
if($finalDirty.Count){throw 'Pipeline validation modified versioned worktree'}
$finalSha=git -C $repo rev-parse HEAD
if($LASTEXITCODE -ne 0 -or $finalSha -ne $sha){throw 'HEAD changed or became unavailable during pipeline validation'}
if($checks.Count -ne $expectedChecks.Count){throw 'Not every existing pipeline executed'}
if($failed){Save-PipelineManifest 'FAILED' 'One or more existing pipelines returned a nonzero exit code.';exit 1}
Save-PipelineManifest 'COMPLETED' $null
exit 0
}
catch{
    $failure=$_.Exception.Message
    if($null -ne $activeCheck){$checks.Add([pscustomobject]@{name=$activeCheck.name;command=$activeCheck.command;exitCode=$null;log=$activeCheck.log;outcome='FAILED';error=$failure})}
    Save-PipelineManifest 'FAILED' $failure
    throw
}
