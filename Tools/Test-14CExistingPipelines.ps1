param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$results=Join-Path $repo 'TestResults'
New-Item -ItemType Directory -Force -Path $results | Out-Null
$sha=git -C $repo rev-parse HEAD
$checks=[System.Collections.Generic.List[object]]::new()
$shell=(Get-Command pwsh -ErrorAction Stop).Source
$failed=$false
foreach($name in @('Test-VisualPipeline','Test-BattlePipeline','Test-EncounterContractPipeline','Test-AIPipeline','Test-PresentationPipeline','Test-IntegrationPipeline','Test-WorldMapPipeline','Test-HistoricalContentPipeline','Test-SaveHardeningPipeline','Test-LongRunPipeline','Build-Windows-Development')){
    $script=Join-Path $PSScriptRoot ($name+'.ps1')
    $command="& '"+$script.Replace("'","''")+"'"
    if($name -notin @('Test-SaveHardeningPipeline','Test-LongRunPipeline')){$command+=" -UnityEditor '"+$UnityEditor.Replace("'","''")+"'"}
    # Existing scripts remain unchanged. Set the native helper visibility in the
    # child session; preserve each script's exact filter, build and exit behavior.
    $body='$PSDefaultParameterValues[''Start-Process:WindowStyle'']=''Hidden''; '+$command+'; exit $LASTEXITCODE'
    $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($body))
    $log=Join-Path $results ('14c-existing-'+$name+'.log')
    & $shell -NoProfile -EncodedCommand $encoded 2>&1 | Tee-Object -FilePath $log | Out-Host
    $code=$LASTEXITCODE
    $checks.Add([pscustomobject]@{name=$name;command=$command;exitCode=$code;log=$log})
    if($code -ne 0){$failed=$true}
    [pscustomobject]@{commitSha=$sha;endSha=(git -C $repo rev-parse HEAD);checks=$checks;worktree=(git -C $repo status --porcelain)} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $results '14c-existing-pipelines.json') -Encoding utf8
    Write-Output "FOC_14C_EXISTING_PIPELINE $name exit=$code sha=$sha"
}
if($failed){exit 1}
exit 0
