param([string]$Blender)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$inputRoot = Join-Path $repo 'Artifacts\ArtInputs'
$sourceRoot = Join-Path $repo 'ArtSource\HistoricalSlice\Upstream'
New-Item -ItemType Directory -Force -Path $inputRoot, $sourceRoot | Out-Null
$files = @('riggedHorse.blend','base.obj','male-young.target','male-build.target','default.mhskel','default_weights.mhw','LICENSE.ASSETS.md')
foreach ($file in $files) {
    $source = Join-Path $sourceRoot $file
    if (-not (Test-Path -LiteralPath $source)) { throw "Versioned source missing: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $inputRoot $file) -Force
}
if (-not $Blender) {
    $command = Get-Command blender -ErrorAction SilentlyContinue
    if ($command) { $Blender = $command.Source }
    else { $Blender = Join-Path $repo 'Artifacts\DccTools\blender-4.5.9-windows-x64\blender.exe' }
}
if (-not (Test-Path -LiteralPath $Blender)) { throw 'Blender is required. Use an official verified portable distribution; no Unity template flow is involved.' }
foreach ($script in @('build_horse_candidate.py','build_human_candidates.py','build_equipment_candidates.py')) {
    & $Blender --background --factory-startup --disable-autoexec --python (Join-Path $PSScriptRoot $script)
    if ($LASTEXITCODE -ne 0) { throw "DCC generation failed: $script" }
}
Write-Output 'FOC_14C_DCC_DRAFTS_GENERATED — this is not production acceptance.'
