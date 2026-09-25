param()

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repoRoot 'UnityProject\Assets\FOC\Content\Resources\FOC\Geography'
$assetPath = Join-Path $assetRoot 'MarmaraStrategyMap.png'
$metaPath = $assetPath + '.meta'
$provenancePath = Join-Path $assetRoot 'MarmaraStrategyMap.provenance.json'

foreach ($path in @($assetPath, $metaPath, $provenancePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "World-map asset input is missing: $path" }
}

$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
$hash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash
if ($hash -ne $provenance.sha256) { throw 'World-map SHA-256 does not match committed provenance.' }
if ($provenance.status -ne 'PRODUCTION_CANDIDATE' -or $provenance.simulationAuthority -ne $false -or $provenance.copiedHistoricalMap -ne $false) {
    throw 'World-map provenance status or authority flags are invalid.'
}
if ($null -eq $provenance.sourceImages -or $provenance.sourceImages.Count -ne 0) { throw 'Original map art must not claim copied source imagery.' }
$meta = Get-Content -LiteralPath $metaPath -Raw
if ($meta -notmatch '(?m)^\s*nPOTScale:\s*0\s*$') { throw 'World-map import must preserve the authored non-power-of-two dimensions.' }

Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($assetPath)
try {
    if ($image.Width -ne 1536 -or $image.Height -ne 1024) { throw "Unexpected world-map dimensions: $($image.Width)x$($image.Height)" }
}
finally { $image.Dispose() }

Write-Output "FOC_WORLD_MAP_ASSET_PASS sha256=$hash dimensions=1536x1024"
