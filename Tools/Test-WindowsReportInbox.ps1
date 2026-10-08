param(
    [string]$PlayerPath,
    [ValidateSet(1366,1920,2560)][int]$Width=1920,
    [ValidateSet(768,1080,1440)][int]$Height=1080,
    [switch]$AllowDirty
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $PlayerPath){$PlayerPath=Join-Path $repo 'Artifacts\WindowsDevelopment\FallOfCavalry.exe'}
$PlayerPath=(Resolve-Path -LiteralPath $PlayerPath).Path
$sha=git -C $repo rev-parse HEAD
if($LASTEXITCODE -ne 0){throw 'Cannot resolve HEAD'}
$beforeDirty=@(git -C $repo status --porcelain)
if($LASTEXITCODE -ne 0){throw 'Cannot read git status'}
if($beforeDirty.Count -and -not $AllowDirty){throw 'Clean worktree required. Use -AllowDirty for development evidence only.'}
$assembly=Join-Path (Split-Path -Parent $PlayerPath) 'FallOfCavalry_Data\Managed\FOC.Bootstrap.Unity.dll'
if((Get-Item -LiteralPath $assembly).LastWriteTimeUtc -lt (Get-Item -LiteralPath (Join-Path $repo 'UnityProject\Assets\FOC\Bootstrap\Runtime\DevelopmentReportInboxAcceptance.cs')).LastWriteTimeUtc){throw 'Stale player. Rebuild Windows Development.'}
$settings=Get-Content -LiteralPath (Join-Path $repo 'UnityProject\ProjectSettings\ProjectSettings.asset') -Raw
$company=[regex]::Match($settings,'(?m)^\s*companyName:\s*(.+)$').Groups[1].Value.Trim().Trim('"')
$product=[regex]::Match($settings,'(?m)^\s*productName:\s*(.+)$').Groups[1].Value.Trim().Trim('"')
foreach($name in @($company,$product)){if(-not $name -or $name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){throw 'Cannot determine player save root safely'}}
$appData=Split-Path -Parent ([Environment]::GetFolderPath('LocalApplicationData'))
$normalSaves=Join-Path $appData "LocalLow\$company\$product\FOC\VerticalSliceSaves"
function Snapshot-Saves {
    $items=@()
    if(Test-Path -LiteralPath $normalSaves){
        $items=@(Get-ChildItem -LiteralPath $normalSaves -File -Recurse | Sort-Object FullName | ForEach-Object {
            [ordered]@{path=$_.FullName;length=$_.Length;modified=$_.LastWriteTimeUtc.ToString('o');hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
        })
    }
    ConvertTo-Json -InputObject $items -Compress -Depth 4
}
$before=Snapshot-Saves
$id=[Guid]::NewGuid().ToString('N')
$saveRoot=Join-Path ([IO.Path]::GetTempPath()) ('foc-windows-smoke\'+$id)
$output=Join-Path $repo "TestResults\WindowsReportInbox\$sha\$id"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$log=Join-Path $output 'player.log'
$arguments=@('-batchmode','-force-d3d11','-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,'-focReportInboxAcceptance','-focSmokeSaveRoot',('"'+$saveRoot+'"'),'-logFile',('"'+$log+'"'))
$process=$null;$exitCode=$null;$failure=$null;$report=$null;$started=[DateTime]::UtcNow;$ownedId=$null
Write-Output "FOC_REPORT_INBOX_START sha=$sha evidence=$output"
try{
    $process=Start-Process -FilePath $PlayerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $ownedId=$process.Id
    if(-not $process.WaitForExit(180000)){throw 'ReportInbox player timed out'}
    $process.Refresh();$exitCode=$process.ExitCode
    $reportPath=Join-Path $saveRoot 'report-inbox.json'
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $output 'report-inbox.json')
    foreach($file in @('01-startup-empty.png','02-delivered-inbox.png','03-city-detail.png','04-turkish-search.png','05-no-results.png','06-trade-range-detail.png','07-reloaded-detail.png')){
        $source=Join-Path $saveRoot $file
        if((Get-Item -LiteralPath $source).Length -le 0){throw "Missing screenshot $file"}
        Copy-Item -LiteralPath $source -Destination (Join-Path $output $file)
        $pixels=[IO.File]::ReadAllBytes($source)
        if($pixels.Length -lt 24 -or [BitConverter]::ToString($pixels,0,8) -ne '89-50-4E-47-0D-0A-1A-0A'){throw "Invalid PNG $file"}
        $actualWidth=([uint32]$pixels[16]*16777216)+([uint32]$pixels[17]*65536)+([uint32]$pixels[18]*256)+$pixels[19]
        $actualHeight=([uint32]$pixels[20]*16777216)+([uint32]$pixels[21]*65536)+([uint32]$pixels[22]*256)+$pixels[23]
        if($actualWidth -ne $Width -or $actualHeight -ne $Height){throw "Render size mismatch in $file requested=${Width}x${Height} actual=${actualWidth}x${actualHeight}"}
    }
    if($report.renderWidth -ne $Width -or $report.renderHeight -ne $Height){throw 'Actual panel target dimensions differ from requested test dimensions'}
    if($exitCode -ne 0 -or $report.status -ne 'PASS' -or $report.graphicsDevice -eq 'Null' -or $report.readOnlyFlows -ne 1 -or $report.saveRoundtrips -ne 1 -or $report.screenshots -ne 7 -or -not $report.normalStartupUnchanged -or $report.saveVersion -ne 14){throw "ReportInbox gate failed: exit=$exitCode status=$($report.status) error=$($report.error)"}
    if(-not (Get-Content -LiteralPath $log -Raw).Contains('FOC_REPORT_INBOX_PASS root='+$saveRoot)){throw 'Successful player marker missing'}
}catch{$failure=$_.Exception.Message}
finally{if($process){if(-not $process.HasExited){Stop-Process -Id $process.Id};$process.Dispose()}}
$after=Snapshot-Saves
$endSha=git -C $repo rev-parse HEAD;$shaExit=$LASTEXITCODE
$afterDirty=@(git -C $repo status --porcelain);$statusExit=$LASTEXITCODE
if($before -cne $after){$failure='Normal player saves changed'}
if($shaExit -ne 0 -or $statusExit -ne 0 -or $endSha -ne $sha){$failure='Commit changed or Git evidence unavailable'}
if($afterDirty.Count -and -not $AllowDirty){$failure='Final worktree is dirty'}
$status=if($failure){'FAIL'}elseif($beforeDirty.Count -or $afterDirty.Count){'DEVELOPMENT_PASS'}else{'PASS'}
$manifest=Join-Path $output 'manifest.json'
[ordered]@{
    status=$status;startSha=$sha;endSha=$endSha;startedUtc=$started.ToString('o');completedUtc=[DateTime]::UtcNow.ToString('o')
    command=$PlayerPath+' '+($arguments -join ' ');processId=$ownedId;exitCode=$exitCode;width=$Width;height=$Height
    playerSha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    isolatedSaveRoot=$saveRoot;normalPlayerSavesUnchanged=($before -ceq $after);error=$failure
    worktree=if($afterDirty.Count){'dirty'}else{'clean'};report=$report
    input='Synthetic UI Toolkit controls, not physical OS input';artAcceptance=$false
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
Write-Output "FOC_REPORT_INBOX_RESULT $status $manifest"
if($failure){throw $failure}
