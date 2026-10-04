param(
    [Parameter(Mandatory=$true)][string]$CaptureDirectory,
    [switch]$AllowDiagnosticSnapshot
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot

function Get-RequiredViews($Entries){
    $views=[Collections.Generic.List[object]]::new()
    foreach($motion in @(@{name='Idle';label='idle';first=1},@{name='OneHandedAttack';label='attack';first=3},@{name='Crouch';label='crouch';first=5})){
        foreach($scenario in @('A','B')){
            $index=$motion.first+$(if($scenario -eq 'B'){1}else{0})
            $label=if($scenario -eq 'A'){'original'}else{'calibrated'}
            $matches=@($Entries | Where-Object {$_.calibrationScenario -eq $scenario -and $_.clip.EndsWith('_'+$motion.name,[StringComparison]::Ordinal) -and [Math]::Abs([double]$_.normalizedPhase-.5) -lt .00001})
            if($matches.Count -ne 1){throw ('Exactly one actual '+$scenario+'/'+$motion.name+' phase .5 capture is required.')}
            $views.Add([pscustomobject]@{path=('{0:00}-{1}-avatar-{2}.png' -f $index,$label,$motion.label);entry=$matches[0];status='CAPTURED_NOT_VISUALLY_ACCEPTED';reason=$null})
        }
    }
    foreach($motion in @(@{source='Walking';label='walk';first=7},@{source='Running';label='run';first=9})){
        foreach($sample in @(@{phase=.25;label='a';offset=0},@{phase=.75;label='b';offset=1})){
            $matches=@($Entries | Where-Object {$_.calibrationScenario -in @('B','C') -and $_.sourceClipName -eq $motion.source -and $_.groundingApplied -eq $true -and [Math]::Abs([double]$_.normalizedPhase-$sample.phase) -lt .00001})
            # B is the single-target calibration. A C fallback is explicitly
            # recorded as dual calibration, never described as a B result.
            $selected=@($matches | Where-Object {$_.calibrationScenario -eq 'B'})
            if(-not $selected.Count){$selected=@($matches | Where-Object {$_.calibrationScenario -eq 'C'})}
            if($selected.Count -gt 1){throw 'Ambiguous contact candidate; choose a single measured candidate before publication.'}
            $views.Add([pscustomobject]@{path=('{0:00}-calibrated-{1}-contact-{2}.png' -f ($motion.first+$sample.offset),$motion.label,$sample.label);entry=if($selected.Count){$selected[0]}else{$null};status=if($selected.Count){'CAPTURED_NOT_VISUALLY_ACCEPTED'}else{'NOT_RUN'};reason=if($selected.Count){$null}else{'No calibrated contact candidate at this phase has groundingApplied=true. Raw locomotion is retained only under Additional.'}})
        }
    }
    return $views.ToArray()
}
function New-ImageRecord([string]$Path,$Entry,[string]$Hash){
    return [pscustomobject]@{
        path=$Path;sourceFile=$Entry.file;sha256=$Hash;scenario=$Entry.calibrationScenario
        scenarioMeaning=switch($Entry.calibrationScenario){'A'{'Original target Avatar'}'B'{'Calibrated target Avatar'}'C'{'Calibrated source AND target Avatars'}}
        sourceClipName=$Entry.sourceClipName;runtimeClipName=$Entry.clip;avatarName=$Entry.avatarName
        normalizedPhase=$Entry.normalizedPhase;groundingApplied=$Entry.groundingApplied;realWindowsPlayer=$Entry.realWindowsPlayer
    }
}
function Copy-ExactImage([string]$SourceFile,[string]$TargetFile){
    Copy-Item -LiteralPath $SourceFile -Destination $TargetFile
    $hash=(Get-FileHash -LiteralPath $SourceFile -Algorithm SHA256).Hash.ToLowerInvariant()
    if((Get-FileHash -LiteralPath $TargetFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){throw 'Evidence copy changed image bytes.'}
    return $hash
}

$source=(Resolve-Path -LiteralPath $CaptureDirectory).Path
$player=Get-Content -LiteralPath (Join-Path $source 'calibration-player-evidence.json') -Raw | ConvertFrom-Json
if($player.sourceSha -notmatch '^[0-9a-f]{40}$' -or $player.platform -ne 'WindowsPlayer' -or $player.status -ne 'TECHNICAL_CAPTURE_NOT_CALIBRATION_ACCEPTANCE' -or $player.activeLeases -ne 0){throw 'Expected completed actual Windows calibration evidence with an explicit source HEAD label.'}
$entries=@($player.results)
if($entries.Count -ne $player.captures -or $entries.Count -lt 8){throw 'Calibration capture/result counts are inconsistent.'}
$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($entry in $entries){
    if(-not $entry.file -or [IO.Path]::GetFileName($entry.file) -ne $entry.file -or [IO.Path]::GetExtension($entry.file) -ne '.png' -or -not $names.Add($entry.file)){throw 'Unsafe or duplicate capture filename.'}
    if(-not(Test-Path -LiteralPath (Join-Path $source $entry.file) -PathType Leaf)){throw ('Missing actual PNG: '+$entry.file)}
    if($entry.realWindowsPlayer -ne $true -or $entry.calibrationScenario -notin @('A','B','C') -or -not $entry.sourceClipName -or -not $entry.clip -or -not $entry.avatarName -or $entry.groundingApplied -isnot [bool]){throw ('Incomplete source/scenario/grounding metadata: '+$entry.file)}
    if($null -eq $entry.normalizedPhase -or [double]::IsNaN([double]$entry.normalizedPhase) -or [double]::IsInfinity([double]$entry.normalizedPhase)){throw 'Missing or invalid capture phase.'}
}
if(@(Get-ChildItem -LiteralPath $source -Filter '*.png' -File).Count -ne $entries.Count){throw 'Unindexed or missing capture images.'}
$runPath=Join-Path (Split-Path -Parent $source) 'run.json'
$run=if(Test-Path -LiteralPath $runPath){Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json}else{$null}
$cleanFinal=$null -ne $run -and $run.result -eq 'PASS' -and $run.runId -and $run.startSha -eq $player.sourceSha -and $run.endSha -eq $player.sourceSha -and @($run.initialDirty).Count -eq 0 -and @($run.finalDirty).Count -eq 0 -and -not $run.gitError -and @($run.notRun).Count -eq 0 -and @($run.checks).Count -eq 4 -and @($run.checks | Where-Object {$_.outcome -ne 'PASS' -or $null -eq $_.exitCode -or $_.exitCode -ne 0}).Count -eq 0 -and $run.player.sourceSha -eq $player.sourceSha -and $run.player.captures -eq $player.captures
if(-not $cleanFinal -and -not $AllowDiagnosticSnapshot){throw 'A clean successful calibration run.json is required. Explicit -AllowDiagnosticSnapshot only publishes a non-final visual-review snapshot.'}
if($run -and ($run.startSha -ne $player.sourceSha -or $run.endSha -ne $player.sourceSha)){throw 'Run and image source SHA labels disagree; diagnostic mode does not waive provenance mismatch.'}
$views=@(Get-RequiredViews $entries)
$publicationId=[Guid]::NewGuid().ToString('N')
$staging=Join-Path $repo ('TestResults/MeshyCalibration/PublicationStaging/'+$publicationId)
$target=Join-Path $repo 'Docs/Evidence/Implementation14C/MeshyHasanCalibration'
New-Item -ItemType Directory -Force -Path $staging,(Join-Path $staging 'Additional') | Out-Null
$required=[Collections.Generic.List[object]]::new();$additional=[Collections.Generic.List[object]]::new()
foreach($view in $views){
    $image=$null
    if($view.entry){$hash=Copy-ExactImage (Join-Path $source $view.entry.file) (Join-Path $staging $view.path);$image=New-ImageRecord $view.path $view.entry $hash}
    $required.Add([pscustomobject]@{path=$view.path;status=$view.status;reason=$view.reason;image=$image})
}
foreach($entry in $entries){
    $relative='Additional/'+$entry.file
    $hash=Copy-ExactImage (Join-Path $source $entry.file) (Join-Path $staging $relative)
    $additional.Add((New-ImageRecord $relative $entry $hash))
}
Copy-Item -LiteralPath (Join-Path $source 'calibration-player-evidence.json') -Destination (Join-Path $staging 'calibration-player-evidence.json')
if($run){Copy-Item -LiteralPath $runPath -Destination (Join-Path $staging 'validation-run.json')}
$editorReport=Join-Path (Split-Path -Parent $source) 'calibration.json'
if(Test-Path -LiteralPath $editorReport){Copy-Item -LiteralPath $editorReport -Destination (Join-Path $staging 'calibration.json')}
$manifest=[ordered]@{
    status='REAL_WINDOWS_REVIEW_SNAPSHOT_NOT_CALIBRATION_ACCEPTANCE';publicationId=$publicationId;publishedUtc=[DateTime]::UtcNow.ToString('o')
    sourceSha=$player.sourceSha;sourceDirectory=$source;sourceRunId=if($run){$run.runId}else{$null}
    cleanFinalRunEvidence=[bool]$cleanFinal;explicitDiagnosticSnapshot=[bool]$AllowDiagnosticSnapshot
    evidenceKind=if($cleanFinal){'CLEAN_SOURCE_SHA_CAPTURE_SNAPSHOT'}else{'SOURCE_HEAD_LABEL_ONLY_NOT_FINAL_SHA_EVIDENCE'}
    requiredCaptured=@($required | Where-Object {$_.image}).Count;requiredTotal=10;requiredViews=$required.ToArray();additional=$additional.ToArray()
    contactStatus=$player.contactStatus;mountStatus=$player.mountStatus;benchmarkStatus=$player.benchmarkStatus
    visualAcceptance='NOT_DECIDED_BY_PUBLICATION';noRetouch=$true;productionActivated=$false
    note='Images are byte-exact Windows-player captures. Required contact views are never substituted with raw clips. Scenario C means dual calibration. This versioned review snapshot records its actual source run, not the commit that later stores it; fresh final-SHA test evidence must be generated after subsequent commits.'
}
$manifest | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $staging 'capture-manifest.json') -Encoding utf8

# Preserve previous managed evidence rather than leave stale contact pictures
# beside a new NOT_RUN manifest. Never touch unrelated notes or subdirectories.
$backup=Join-Path $repo ('TestResults/MeshyCalibration/PublicationBackups/'+$publicationId)
New-Item -ItemType Directory -Force -Path $target,(Join-Path $target 'Additional'),$backup,(Join-Path $backup 'Additional') | Out-Null
$managed=@($views.path)+@('capture-manifest.json','calibration-player-evidence.json','validation-run.json','calibration.json')
try{
    foreach($name in $managed){$existing=Join-Path $target $name;if(Test-Path -LiteralPath $existing -PathType Leaf){Move-Item -LiteralPath $existing -Destination (Join-Path $backup $name)}}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $target 'Additional') -Filter '*.png' -File){Move-Item -LiteralPath $file.FullName -Destination (Join-Path (Join-Path $backup 'Additional') $file.Name)}
    [ordered]@{status='PUBLISHING_NOT_ACCEPTED';publicationId=$publicationId;sourceSha=$player.sourceSha;backup=$backup} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'capture-manifest.json') -Encoding utf8
    foreach($file in Get-ChildItem -LiteralPath $staging -File | Where-Object {$_.Name -ne 'capture-manifest.json'}){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name)}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $staging 'Additional') -File){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path (Join-Path $target 'Additional') $file.Name)}
    foreach($record in @($required | Where-Object {$_.image} | ForEach-Object {$_.image})+$additional.ToArray()){
        if((Get-FileHash -LiteralPath (Join-Path $target $record.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $record.sha256){throw 'Published image differs from the exact staged source.'}
    }
    Copy-Item -LiteralPath (Join-Path $staging 'capture-manifest.json') -Destination (Join-Path $target 'capture-manifest.json')
}catch{
    [ordered]@{status='PUBLICATION_FAILED_NOT_ACCEPTED';publicationId=$publicationId;sourceSha=$player.sourceSha;error=$_.Exception.Message;backup=$backup;staging=$staging} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'capture-manifest.json') -Encoding utf8
    throw
}
Write-Output "MESHY_CALIBRATION_REVIEW_PUBLISHED path=$target required=$($manifest.requiredCaptured)/10 cleanSourceRun=$cleanFinal previousEvidenceBackup=$backup"
