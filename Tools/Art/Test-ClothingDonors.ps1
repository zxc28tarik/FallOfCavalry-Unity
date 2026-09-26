param([Parameter(Mandatory=$true)][string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$output=Join-Path $repo 'TestResults/ClothingDonors'
New-Item -ItemType Directory -Force -Path $output | Out-Null
foreach($step in @(@('FOC.Editor.Visuals.ClothingDonorValidation.Run','unity-donor.log'),@('FOC.Editor.Visuals.HistoricalArtReviewBuild.RunDonors','unity-donor-build.log'))){
    $p=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-nographics','-projectPath',('"'+$repo+'/UnityProject"'),'-executeMethod',$step[0],'-logFile',('"'+(Join-Path $output $step[1])+'"')) -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if($p.ExitCode -ne 0){throw "$($step[0]) failed: $($p.ExitCode)"}
    Write-Output "$($step[0]) exit=0"
}
$player=Join-Path $repo 'Artifacts/ArtReviewPlayer/FallOfCavalry-ArtReview.exe'
Add-Type -AssemblyName System.Drawing
$results=@()
foreach($asset in @('HasanRobe','TunicAlternative','SipahiRanger','CebeliPeasant','PeasantLegs','RangerLegs')){
    foreach($pose in @('standing','mounted')){
        $label=$asset+'-'+$pose
        $bmp=Join-Path $output ($label+'.bmp');$log=Join-Path $output ($label+'.log')
        $p=Start-Process -FilePath $player -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','900','-focArtAsset',('CHR_Donor_'+$asset),'-focArtAngle','front','-focArtPose',$pose,'-focScreenshotPath',('"'+$bmp+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
        if(-not $p.WaitForExit(60000)){throw "Capture timeout PID=$($p.Id)"}
        if($p.ExitCode -ne 0 -or -not(Select-String -LiteralPath $log -Pattern 'FOC_ART_DRAFT_PLAYER_CAPTURE' -Quiet)){throw "Capture failed $label"}
        $image=[Drawing.Image]::FromFile($bmp)
        try{$image.Save((Join-Path $output ($label+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
        $results+=@{asset=$asset;pose=$pose;exitCode=$p.ExitCode;status='CAPTURED_NOT_VISUALLY_ACCEPTED';image=$label+'.png'}
        Write-Output "DONOR_CAPTURE $label exit=0"
    }
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'capture-results.json') -Encoding utf8
Write-Output 'DONOR_DIAGNOSTICS_COMPLETE_NOT_PRODUCTION_ACCEPTANCE'
