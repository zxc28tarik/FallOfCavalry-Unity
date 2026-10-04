[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'))

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$intake = [IO.Path]::GetFullPath((Join-Path $repository 'Artifacts/MotionLibraryIntake'))
$archive = Join-Path $intake 'QuaterniusStandard.zip'
$officialPage = 'https://quaternius.itch.io/universal-animation-library'
New-Item -ItemType Directory -Path $intake -Force | Out-Null
if (Test-Path -LiteralPath $archive) {
    throw "Refusing to overwrite existing intake archive: $archive"
}

# Anonymous official free Standard flow. No account, payment, or paid Source tier.
$page = Invoke-WebRequest -Uri $officialPage -SessionVariable motionSession
$csrf = [regex]::Match($page.Content, '<meta name="csrf_token" value="([^"]+)"').Groups[1].Value
if (-not $csrf) { throw 'Official page did not expose its anonymous CSRF token.' }
$download = Invoke-RestMethod -Uri "$officialPage/download_url" -Method Post -WebSession $motionSession -Body @{csrf_token=$csrf}
if (-not $download.url) { throw 'Official free download page was not returned.' }
$listing = Invoke-WebRequest -Uri $download.url -WebSession $motionSession
$standardBlocks = @([regex]::Split($listing.Content, '<div class="upload">') | Where-Object {$_ -match 'title="Universal Animation Library\[Standard\]\.zip"'})
if ($standardBlocks.Count -ne 1) { throw 'Expected exactly one free Standard download entry.' }
$match = [regex]::Match($standardBlocks[0], 'data-upload_id="(\d+)"')
if (-not $match.Success) { throw 'Free Standard ZIP is not present; do not substitute a paid tier.' }
$uploadId = $match.Groups[1].Value
$csrf = [regex]::Match($listing.Content, '<meta name="csrf_token" value="([^"]+)"').Groups[1].Value
if (-not $csrf) { throw 'Download page CSRF token missing.' }
$endpoint = "$officialPage/file/${uploadId}?source=game_download"
$file = Invoke-RestMethod -Uri $endpoint -Method Post -WebSession $motionSession -Body @{csrf_token=$csrf}
if (-not $file.url -or $file.external) { throw 'Expected an official internal itch.io file URL.' }
Invoke-WebRequest -Uri $file.url -OutFile $archive
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$destination = [IO.Path]::GetFullPath((Join-Path $intake ('QuaterniusStandard-' + $archiveHash.Substring(0, 12))))
if (-not $destination.StartsWith($intake + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid extraction root.' }
if (Test-Path -LiteralPath $destination) { throw "Refusing to merge into existing extraction directory: $destination" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    # Validate every path BEFORE extracting anything. ZIP traversal/absolute paths
    # and duplicate destinations are rejected, including case-insensitive clashes.
    $validated = @()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $zip.Entries) {
        $relative = $entry.FullName.Replace('/', [IO.Path]::DirectorySeparatorChar)
        if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':')) { throw "Unsafe ZIP path: $($entry.FullName)" }
        $target = [IO.Path]::GetFullPath((Join-Path $destination $relative))
        if (-not $target.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "ZIP traversal: $($entry.FullName)" }
        if (-not $seen.Add($target)) { throw "Duplicate ZIP destination: $($entry.FullName)" }
        $validated += [pscustomobject]@{Entry=$entry;Target=$target}
    }
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($item in $validated) {
        if ($item.Entry.FullName.EndsWith('/')) { New-Item -ItemType Directory -Path $item.Target -Force | Out-Null; continue }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($item.Target)) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($item.Entry, $item.Target, $false)
    }
    [pscustomobject]@{
        officialPage=$officialPage; officialFileEndpoint=$endpoint; uploadId=$uploadId
        archive=$archive; archiveBytes=(Get-Item -LiteralPath $archive).Length
        archiveSha256=$archiveHash; extractionRoot=$destination
        files=@($zip.Entries | Where-Object {-not $_.FullName.EndsWith('/')} | ForEach-Object {
            [pscustomobject]@{name=$_.FullName;bytes=$_.Length;compressedBytes=$_.CompressedLength}
        })
    } | ConvertTo-Json -Depth 5
}
finally { $zip.Dispose() }
