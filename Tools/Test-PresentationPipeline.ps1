param(
    [Parameter(Mandatory = $true)]
    [string] $UnityEditor
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'UnityProject'
$resultsRoot = Join-Path $repoRoot 'TestResults'
$resultsPath = Join-Path $resultsRoot 'presentation-editmode.xml'
$testLogPath = Join-Path $resultsRoot 'presentation-editmode.log'
$pipelineLogPath = Join-Path $resultsRoot 'presentation-pipeline.log'
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null

dotnet test (Join-Path $repoRoot 'FallOfCavalry.sln') --configuration Release --filter 'FullyQualifiedName~Presentation' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testArguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', "`"$projectPath`"",
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testFilter', 'FOC.Tests.Presentation',
    '-testResults', "`"$resultsPath`"",
    '-logFile', "`"$testLogPath`""
)
$testProcess = Start-Process -FilePath $UnityEditor -ArgumentList $testArguments -Wait -PassThru
if ($testProcess.ExitCode -ne 0) { exit $testProcess.ExitCode }

$pipelineArguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', "`"$projectPath`"",
    '-executeMethod', 'FOC.Editor.Presentation.PresentationPipelineBatch.Run',
    '-logFile', "`"$pipelineLogPath`""
)
$pipelineProcess = Start-Process -FilePath $UnityEditor -ArgumentList $pipelineArguments -Wait -PassThru
exit $pipelineProcess.ExitCode
