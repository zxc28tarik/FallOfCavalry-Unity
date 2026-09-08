param(
    [Parameter(Mandatory = $true)]
    [string] $UnityEditor
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'UnityProject'
$resultsPath = Join-Path $repoRoot 'TestResults\unity-editmode.xml'
$logPath = Join-Path $repoRoot 'TestResults\unity-editmode.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resultsPath) | Out-Null

& $UnityEditor -batchmode -nographics -quit -projectPath $projectPath -runTests -testPlatform EditMode -testResults $resultsPath -logFile $logPath
exit $LASTEXITCODE

