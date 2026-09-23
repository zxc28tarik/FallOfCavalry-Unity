param(
    [Parameter(Mandatory = $true)]
    [string] $UnityEditor
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'UnityProject'
$resultsRoot = Join-Path $repoRoot 'TestResults'
$resultsPath = Join-Path $resultsRoot 'ai-editmode.xml'
$logPath = Join-Path $resultsRoot 'ai-pipeline.log'
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null

dotnet test (Join-Path $repoRoot 'FallOfCavalry.sln') --configuration Release --filter 'FullyQualifiedName~AIDecisionMakingTests' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', "`"$projectPath`"",
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testFilter', 'FOC.Tests.AIDecisionMakingTests',
    '-testResults', "`"$resultsPath`"",
    '-logFile', "`"$logPath`""
)

$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -Wait -PassThru
exit $process.ExitCode
