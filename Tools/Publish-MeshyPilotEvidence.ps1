param([Parameter(Mandatory=$true)][string]$CaptureDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$source=(Resolve-Path -LiteralPath $CaptureDirectory).Path
$evidence=Join-Path $repo 'Docs/Evidence/Implementation14C/MeshyHasanPilot'
$player=Get-Content -LiteralPath (Join-Path $source 'player-evidence.json') -Raw | ConvertFrom-Json
$runPath=Join-Path (Split-Path -Parent $source) 'run.json'
$runInfo=if(Test-Path -LiteralPath $runPath){Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json}else{$null}
if($player.platform -ne 'WindowsPlayer' -or $player.activeLeases -ne 0){throw 'Only successful real Windows output may be published.'}
$mapping=[ordered]@{
 '01-front.png'='01-rest-front.png';'02-side.png'='02-rest-side.png';'03-three-quarter.png'='03-three-quarter.png'
 '04-walk-a.png'='05-walking-0.25.png';'05-walk-b.png'='07-walking-0.75.png'
 '06-run-a.png'='09-running-0.25.png';'07-run-b.png'='11-running-0.75.png'
 '08-foc-idle-retarget.png'='08-foc-idle-retarget.png';'09-foc-attack-retarget.png'='09-foc-attack-retarget.png'
 '10-foc-crouch-retarget.png'='10-foc-crouch-retarget.png'
}
foreach($name in $mapping.Values){if(-not(Test-Path -LiteralPath (Join-Path $source $name))){throw "Missing actual capture: $name"}}
New-Item -ItemType Directory -Force -Path $evidence,(Join-Path $evidence 'Additional') | Out-Null
$records=[Collections.Generic.List[object]]::new()
foreach($pair in $mapping.GetEnumerator()){
 $original=Join-Path $source $pair.Value;$target=Join-Path $evidence $pair.Key
 Copy-Item -LiteralPath $original -Destination $target
 $hash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
 if($hash -ne (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'Copy changed image bytes.'}
 $records.Add([pscustomobject]@{path=$pair.Key;sourceFile=$pair.Value;sha256=$hash})
}
foreach($image in Get-ChildItem -LiteralPath $source -Filter '*.png'){
 Copy-Item -LiteralPath $image.FullName -Destination (Join-Path (Join-Path $evidence 'Additional') $image.Name)
}
Copy-Item -LiteralPath (Join-Path $source 'player-evidence.json') -Destination (Join-Path $evidence 'player-evidence.json')
# A generated manifest records the actual source label, never substitutes HEAD.
[ordered]@{status='REAL_WINDOWS_CAPTURE_NOT_AUTOMATIC_PILOT_ACCEPTANCE';sourceSha=$player.sourceSha;diagnosticDirtyRun=if($null -ne $runInfo){[bool]$runInfo.diagnosticDirtyRun}else{$null};runId=if($null -ne $runInfo){$runInfo.runId}else{$null};sourceDirectory=$source;images=$records.ToArray();noRetouch=$true;mounted='NOT_RUN_ON_FOOT_ACCEPTANCE_REQUIRED';note='Committed visual-review snapshot. If diagnosticDirtyRun is true, sourceSha identifies the base HEAD, not the modified code contents. Final-SHA test evidence is separately recorded by Test-MeshyPilot in its fresh run directory.'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'capture-manifest.json') -Encoding utf8
Write-Output "MESHY_REAL_EVIDENCE_PUBLISHED $evidence"
