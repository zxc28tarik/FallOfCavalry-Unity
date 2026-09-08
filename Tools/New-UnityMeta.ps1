param(
    [string] $AssetsPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'UnityProject\Assets')
)

$assetsRoot = (Resolve-Path -LiteralPath $AssetsPath).Path
$items = Get-ChildItem -LiteralPath $assetsRoot -Recurse -Force | Where-Object { $_.Name -notlike '*.meta' }

foreach ($item in $items) {
    $relativePath = $item.FullName.Substring($assetsRoot.Length).TrimStart('\', '/').Replace('\', '/')
    $hashBytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($relativePath))
    $guid = ([System.Convert]::ToHexString($hashBytes)).Substring(0, 32).ToLowerInvariant()
    $metaPath = $item.FullName + '.meta'

    if (Test-Path -LiteralPath $metaPath) {
        continue
    }

    if ($item.PSIsContainer) {
        $content = @"
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    }
    elseif ($item.Extension -eq '.cs') {
        $content = @"
fileFormatVersion: 2
guid: $guid
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    }
    elseif ($item.Extension -eq '.asmdef') {
        $content = @"
fileFormatVersion: 2
guid: $guid
AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    }
    else {
        $content = @"
fileFormatVersion: 2
guid: $guid
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    }

    Set-Content -LiteralPath $metaPath -Value $content -Encoding utf8NoBOM
}

