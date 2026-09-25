param([string]$Player,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if(-not $Player){$Player=Join-Path $repo 'Artifacts\ArtReviewPlayer\FallOfCavalry-ArtReview.exe'}
if(-not(Test-Path -LiteralPath $Player)){throw 'Build HistoricalArtReviewBuild.Run first.'}
$output=if($OutputDirectory){$OutputDirectory}else{Join-Path $repo 'Docs\Evidence\Implementation14C\Drafts'}
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName System.Drawing
$shots=@(
    @('horse-three-quarter','MNT_Horse_Anatolian_01','front','Idle'),
    @('horse-side','MNT_Horse_Anatolian_01','side','Idle'),
    @('horse-trot','MNT_Horse_Anatolian_01','side','Trot'),
    @('hasan-body-draft','CHR_HasanAga_01','front',''),
    @('cebeli-body-draft','CHR_Cebeli_01','front',''),
    @('matchlock-draft','WPN_FitilliTufek_01','front',''),
    @('hasan-standing-deformation-draft','CHR_HasanAga_01','front','','standing'),
    @('sipahi-mounted-side-fit-draft','CHR_Sipahi_01','side','','mounted'),
    @('sipahi-mounted-three-quarter-fit-draft','CHR_Sipahi_01','front','','mounted')
)
foreach($shot in $shots){
    $bmp=Join-Path $repo ('TestResults\14c-'+$shot[0]+'.bmp')
    $log=Join-Path $repo ('TestResults\14c-'+$shot[0]+'-player.log')
    $args=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','900','-focArtAsset',$shot[1],'-focArtAngle',$shot[2],'-focScreenshotPath',('"'+$bmp+'"'),'-logFile',('"'+$log+'"'))
    if($shot[3]){$args+=@('-focArtAnimation',$shot[3])}
    if($shot.Length -gt 4 -and $shot[4]){$args+=@('-focArtPose',$shot[4])}
    $process=Start-Process -FilePath $Player -ArgumentList $args -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(45000)){throw "Art player timed out. Inspect PID $($process.Id)."}
    if($process.ExitCode -ne 0){throw "Art player failed: $($shot[0]) exit=$($process.ExitCode)"}
    if(-not(Select-String -LiteralPath $log -Pattern 'FOC_ART_DRAFT_PLAYER_CAPTURE' -Quiet)){throw 'Capture marker missing.'}
    # Lossless format conversion only. No retouching, compositing or generated image.
    $image=[System.Drawing.Image]::FromFile($bmp)
    try{$image.Save((Join-Path $output ($shot[0]+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
    Write-Output "Captured DRAFT $($shot[0]) exit=$($process.ExitCode)"
}
