param(
    [Parameter(Mandatory = $true)]
    [string] $UnityEditor
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'UnityProject'
$resultsRoot = Join-Path $repoRoot 'TestResults'
$logPath = Join-Path $resultsRoot 'visual-pipeline.log'
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', "`"$projectPath`"",
    '-executeMethod', 'FOC.Editor.Visuals.VisualPipelineBatch.Run',
    '-logFile', "`"$logPath`""
)

$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -Wait -PassThru
exit $process.ExitCode
