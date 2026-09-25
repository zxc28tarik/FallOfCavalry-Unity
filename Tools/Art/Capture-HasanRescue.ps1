param([Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$Pose='standing')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$player=Join-Path $repo 'Artifacts\ArtReviewPlayer\FallOfCavalry-ArtReview.exe'
$output=[System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName System.Drawing
foreach($angle in @('straight','side','front')){
    $bmp=Join-Path $output ('hasan-'+$angle+'.bmp')
    $log=Join-Path $output ('hasan-'+$angle+'.log')
    $arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','900','-focArtAsset','CHR_HasanAga_01','-focArtAngle',$angle,'-focScreenshotPath',('"'+$bmp+'"'),'-logFile',('"'+$log+'"'))
    if($Pose){$arguments+=@('-focArtPose',$Pose)}
    $p=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(45000)){throw "Review timed out; inspect PID $($p.Id)"}
    if($p.ExitCode -ne 0){throw "Review failed: $angle code=$($p.ExitCode)"}
    if(-not(Select-String -LiteralPath $log -Pattern 'FOC_ART_DRAFT_PLAYER_CAPTURE' -Quiet)){throw 'Capture marker missing'}
    $image=[System.Drawing.Image]::FromFile($bmp)
    try{$image.Save((Join-Path $output ('hasan-'+$angle+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
    Write-Output "Hasan diagnostic $angle, pose=$Pose, exit=$($p.ExitCode). Not production acceptance."
}
