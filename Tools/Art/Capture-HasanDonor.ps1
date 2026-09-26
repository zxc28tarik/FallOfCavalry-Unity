param([string]$OutputDirectory='TestResults/HasanDonor/Captures')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if(-not [IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path $repo $OutputDirectory}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Add-Type -AssemblyName System.Drawing
$player=Join-Path $repo 'Artifacts/ArtReviewPlayer/FallOfCavalry-ArtReview.exe'
$shots=@(
    @('01-front','straight','Idle','standing','0.25'),
    @('02-side','side','Idle','standing','0.25'),
    @('03-three-quarter','front','Idle','standing','0.25'),
    @('04-walk-a','front','Walk','standing','0.25'),
    @('05-walk-b','front','Walk','standing','0.75'),
    @('06-run','front','Run','standing','0.25'),
    @('07-turn','front','Turn','standing','0.25'),
    @('08-attack-a','front','OneHandedAttack','standing','0.10'),
    @('09-attack-b','front','OneHandedAttack','standing','0.50'),
    @('10-arm-raise','straight','ArmRaise','standing','0.50'),
    @('11-crouch','side','Crouch','standing','0.50'),
    @('12-mounted-side','side','MountedSeated','mounted','0.25'),
    @('13-mounted-three-quarter','front','MountedSeated','mounted','0.25')
)
$results=@()
foreach($shot in $shots){
    $bmp=Join-Path $OutputDirectory ($shot[0]+'.bmp');$log=Join-Path $OutputDirectory ($shot[0]+'.log')
    $arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','900','-focArtAsset','CHR_HasanAga_DonorDraft','-focArtAngle',$shot[1],'-focArtAnimation',$shot[2],'-focArtPose',$shot[3],'-focArtPhase',$shot[4],'-focScreenshotPath',('"'+$bmp+'"'),'-logFile',('"'+$log+'"'))
    if($shot[2] -eq 'OneHandedAttack'){$arguments+=@('-focArtWeapon','WPN_Kilic_01')}
    $p=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(60000)){$p.Kill();throw "Capture timed out PID=$($p.Id): $($shot[0])"}
    if($p.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'FOC_ART_DRAFT_PLAYER_CAPTURE' -Quiet)){throw "Capture failed: $($shot[0]), exit=$($p.ExitCode)"}
    $motion=Select-String -LiteralPath $log -Pattern 'FOC_HASAN_REAL_MOTION' | Select-Object -First 1
    if(-not $motion){throw "No real motion evidence for $($shot[0])"}
    $image=[Drawing.Image]::FromFile($bmp)
    try{$image.Save((Join-Path $OutputDirectory ($shot[0]+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
    $results+=@{image=$shot[0]+'.png';motion=$shot[2];phase=$shot[4];exitCode=$p.ExitCode;runtimeEvidence=$motion.Line;status='DRAFT_REQUIRES_VISUAL_REVIEW';command=('"'+$player+'" '+($arguments -join ' '))}
    Write-Output "HASAN_DONOR_CAPTURE $($shot[0]) exit=0"
}
@{sha=(git -C $repo rev-parse HEAD);worktree=(git -C $repo status --porcelain);diagnosticNotBattleAnimation=$true;captures=$results} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'capture-results.json') -Encoding utf8
